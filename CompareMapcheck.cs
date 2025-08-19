using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.IO;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;
using System.Security.Cryptography;

// TODO: Replace the following version attributes by creating AssemblyInfo.cs. You can do this in the properties of the Visual Studio project.
[assembly: AssemblyVersion("1.0.0.1")]
[assembly: AssemblyFileVersion("1.0.0.1")]
[assembly: AssemblyInformationalVersion("1.0")]

// TODO: Uncomment the following line if the script requires write access.
// [assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
    public class Script
    {
        public Script()
        {
        }

        public class Detector
        {
            public VVector Position;
            public double Value;
        }

        public static double threshold = 0.1;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context /*, System.Windows.Window window, ScriptEnvironment environment*/)
        {

            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Filter = "Text Files (*.txt)|*.txt";
            fileDialog.Title = "Select the MapCHECK measurement file...";
            fileDialog.Multiselect = false;
            var success = fileDialog.ShowDialog();

            // If the user did not select a file
            if (!(bool)success) { return; }

            string fileName = fileDialog.FileName;

            // Initialize list to store parsed lines
            List<Detector> measurements = new List<Detector>();

            // Initialize max dose variable, flag, and stream buffer variables
            double maxDose = 0;
            bool doseCounts = false;
            const Int32 BufferSize = 128;

            // Define regular expression to detect lines that contain interplated dose points
            Regex r = new Regex(new StringBuilder().Insert(0, @"\t([-\d\.]+)", 46).Insert(0, @"([-\d\.]+)").ToString());

            using (var fileStream = File.OpenRead(fileName))
            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8, true, BufferSize))
            {

                // Read the next line from the file into a string, until the end of the file
                String line;
                while ((line = streamReader.ReadLine()) != null)
                {

                    if (line == "Dose Counts") { 
                        doseCounts  = true; 
                        continue; 
                    }

                    if (line == "Data Flags") { 
                        doseCounts = false; 
                        continue; 
                    }

                    if (!doseCounts) { 
                        continue; 
                    }

                    // Try to match the line to the profile point pattern above
                    Match m = r.Match(line);
                    if (m.Success)
                    {
                        // Parse out y value
                        Double.TryParse(m.Groups[1].Value, out double y);

                        // Loop through detectors and save any non-zero values
                        for (int i = 3; i < m.Groups.Count; i++)
                        {

                            // Parse out dose value
                            Double.TryParse(m.Groups[i].Value, out double v);

                            if (v > 0)
                            {
                                Detector d = new Detector
                                {
                                    Position = new VVector(-110 + (i - 3) * 5, 0, y * 10),
                                    Value = v
                                };

                                if (v > maxDose)
                                {
                                    maxDose = v;
                                }

                                measurements.Add(d);
                            }

                        }
                    }

                }
            }

            // Initialize results metrics
            double[] passingDetectors = new double[] { 0, 0, 0, 0 };
            double aboveThreshold = 0;
            double sumDiff = 0;
            double normDiff = 0;

            // Loop through measurements
            foreach (Detector d in measurements)
            {
                if (d.Value < maxDose * threshold) { 
                    continue; 
                }
                else { 
                    aboveThreshold += 1.0; 
                }

                // Initialize gamma squared temporary variable
                double[] gs = new double[] { 100, 100, 100, 100 };

                // Extract dose profile along x axis +/10mm
                VVector start = context.Image.UserToDicom(d.Position + new VVector(-2, 0, 0), context.PlanSetup);
                VVector end = context.Image.UserToDicom(d.Position + new VVector(2, 0, 0), context.PlanSetup);
                DoseProfile tps = context.PlanSetup.Dose.GetDoseProfile(start, end, new double[41]);

                // Calculate dose difference sum
                sumDiff += (tps[21].Value - d.Value) / d.Value;

                // If this is the max value, store normization point difference
                if (d.Value == maxDose)
                {
                    normDiff = (tps[21].Value - d.Value) / d.Value;
                }

                // Loop through profile, converting the coordinates back from DICOM and calculating gamma/difference
                foreach (ProfilePoint p in tps)
                {
                    VVector position = context.Image.DicomToUser(p.Position, context.PlanSetup);

                    // Calculate 2%/2mm global
                    gs[0] = Math.Min(gs[0], Math.Pow((p.Value - d.Value) / (maxDose * 0.02), 2) +
                        (d.Position - position).LengthSquared / Math.Pow(2, 2));

                    // Calculate 2%/2mm local
                    gs[1] = Math.Min(gs[1], Math.Pow((p.Value - d.Value) / (d.Value * 0.02), 2) +
                        (d.Position - position).LengthSquared / Math.Pow(2, 2));

                    // Calculate 1%/1mm global
                    gs[2] = Math.Min(gs[2], Math.Pow((p.Value - d.Value) / (maxDose * 0.01), 2) +
                        (d.Position - position).LengthSquared / Math.Pow(1, 2));

                    // Calculate 1%/1mm local
                    gs[3] = Math.Min(gs[3], Math.Pow((p.Value - d.Value) / (d.Value * 0.01), 2) +
                        (d.Position - position).LengthSquared / Math.Pow(1, 2));
                }

                // Extract dose profile along y axis
                start = context.Image.UserToDicom(d.Position + new VVector(0, 0, -2), context.PlanSetup);
                end = context.Image.UserToDicom(d.Position + new VVector(0, 0, 2), context.PlanSetup);
                tps = context.PlanSetup.Dose.GetDoseProfile(start, end, new double[41]);

                // Loop through profile, converting the coordinates back from DICOM and calculating gamma/difference
                foreach (ProfilePoint p in tps)
                {
                    VVector position = context.Image.DicomToUser(p.Position, context.PlanSetup);

                    // Calculate 2%/2mm global
                    gs[0] = Math.Min(gs[0], Math.Pow((p.Value - d.Value) / (maxDose * 0.02), 2) +
                        (d.Position - position).LengthSquared / Math.Pow(2, 2));

                    // Calculate 2%/2mm local
                    gs[1] = Math.Min(gs[1], Math.Pow((p.Value - d.Value) / (d.Value * 0.02), 2) +
                        (d.Position - position).LengthSquared / Math.Pow(2, 2));

                    // Calculate 1%/1mm global
                    gs[2] = Math.Min(gs[2], Math.Pow((p.Value - d.Value) / (maxDose * 0.01), 2) +
                        (d.Position - position).LengthSquared / Math.Pow(1, 2));

                    // Calculate 1%/1mm local
                    gs[3] = Math.Min(gs[3], Math.Pow((p.Value - d.Value) / (d.Value * 0.01), 2) +
                        (d.Position - position).LengthSquared / Math.Pow(1, 2));
                }

                for (int i = 0; i < gs.Length; i++)
                {
                    if (Math.Sqrt(gs[i]) <= 1)
                    {
                        passingDetectors[i] += 1.0;
                    }
                }
            }

            // Display results
            MessageBox.Show("Normalized TPS Dose Difference: " + (Math.Round(normDiff * 1000) / 10).ToString() + "%\n"
                + "Average TPS Dose Difference: " + (Math.Round(sumDiff / aboveThreshold * 1000) / 10).ToString() + "%\n"
                + "Gamma Pass Rates (" + (threshold * 100).ToString()  + "% threshold): \n"
                + "     2%/2mm Global: " + (Math.Round(passingDetectors[0] / aboveThreshold * 1000) / 10).ToString() + "%\n"
                + "     2%/2mm Local: " + (Math.Round(passingDetectors[1] / aboveThreshold * 1000) / 10).ToString() + "%\n"
                + "     1%/1mm Global: " + (Math.Round(passingDetectors[2] / aboveThreshold * 1000) / 10).ToString() + "%\n"
                + "     1%/1mm Local: " + (Math.Round(passingDetectors[3] / aboveThreshold * 1000) / 10).ToString() + "%", 
                "MapCHECK Comparison Results");
        }
    }
}
