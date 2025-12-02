using System;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.IO;

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

        // Define threshold fraction of max dose for reportng gamma pass rate (0.1 = 10%)
        public static double threshold = 0.1;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context /*, System.Windows.Window window, ScriptEnvironment environment*/)
        {

            // Present user with file selection dialog to select MapCHECK file
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
            bool readData = false;
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
                    // Once the line containing "Dose Counts" is found, start reading data
                    if (line == "Dose Counts") { 
                        readData  = true; 
                        continue; 
                    }

                    // Once the line containing "Data Flags" is found, stop reading data
                    if (line == "Data Flags") { 
                        readData = false; 
                        continue; 
                    }

                    // Skip lines in file if read flag isn't set
                    if (!readData)
                    {
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
                                // Create new detector object containing position and dose data
                                Detector d = new Detector
                                {
                                    Position = new VVector(-110 + (i - 3) * 5, 0, y * 10),
                                    Value = v
                                };

                                // Update maxDose variable to maximum
                                if (v > maxDose)
                                {
                                    maxDose = v;
                                }

                                // Add measurement to list
                                measurements.Add(d);
                            }

                        }
                    }
                }
            }

            // Initialize results metrics
            double[] passingDetectors = new double[] { 0, 0, 0, 0, 0, 0 };
            double aboveThreshold = 0;
            List<double> differences = new List<double>();
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
                double[] gs = new double[] { 100, 100, 100, 100, 100, 100 };

                // Extract dose profile along x axis +/10mm
                VVector start = context.Image.UserToDicom(d.Position + new VVector(-2, 0, 0), context.PlanSetup);
                VVector end = context.Image.UserToDicom(d.Position + new VVector(2, 0, 0), context.PlanSetup);
                DoseProfile tps = context.PlanSetup.Dose.GetDoseProfile(start, end, new double[41]);

                // Add difference to list (in order to calculate median)
                differences.Add((tps[21].Value - d.Value) / d.Value);

                // If this is the max value, store normization point difference
                if (d.Value == maxDose)
                {
                    normDiff = (tps[21].Value - d.Value) / d.Value;
                }

                // Loop through profile, converting the coordinates back from DICOM and calculating gamma/difference
                foreach (ProfilePoint p in tps)
                {
                    VVector position = context.Image.DicomToUser(p.Position, context.PlanSetup);

                    // Calculate 3%/3mm global
                    gs[0] = Math.Min(gs[0], Math.Pow((p.Value - d.Value) / (maxDose * 0.03), 2) +
                        (d.Position - position).LengthSquared / Math.Pow(3, 2));

                    // Calculate 3%/3mm local
                    gs[1] = Math.Min(gs[1], Math.Pow((p.Value - d.Value) / (d.Value * 0.03), 2) +
                        (d.Position - position).LengthSquared / Math.Pow(3, 2));

                    // Calculate 2%/2mm global
                    gs[2] = Math.Min(gs[2], Math.Pow((p.Value - d.Value) / (maxDose * 0.02), 2) +
                        (d.Position - position).LengthSquared / Math.Pow(2, 2));

                    // Calculate 2%/2mm local
                    gs[3] = Math.Min(gs[3], Math.Pow((p.Value - d.Value) / (d.Value * 0.02), 2) +
                        (d.Position - position).LengthSquared / Math.Pow(2, 2));

                    // Calculate 1%/1mm global
                    gs[4] = Math.Min(gs[4], Math.Pow((p.Value - d.Value) / (maxDose * 0.01), 2) +
                        (d.Position - position).LengthSquared / Math.Pow(1, 2));

                    // Calculate 1%/1mm local
                    gs[5] = Math.Min(gs[5], Math.Pow((p.Value - d.Value) / (d.Value * 0.01), 2) +
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

                    // Calculate 3%/3mm global
                    gs[0] = Math.Min(gs[0], Math.Pow((p.Value - d.Value) / (maxDose * 0.03), 2) +
                        (d.Position - position).LengthSquared / Math.Pow(3, 2));

                    // Calculate 3%/3mm local
                    gs[1] = Math.Min(gs[1], Math.Pow((p.Value - d.Value) / (d.Value * 0.03), 2) +
                        (d.Position - position).LengthSquared / Math.Pow(3, 2));

                    // Calculate 2%/2mm global
                    gs[2] = Math.Min(gs[2], Math.Pow((p.Value - d.Value) / (maxDose * 0.02), 2) +
                        (d.Position - position).LengthSquared / Math.Pow(2, 2));

                    // Calculate 2%/2mm local
                    gs[3] = Math.Min(gs[3], Math.Pow((p.Value - d.Value) / (d.Value * 0.02), 2) +
                        (d.Position - position).LengthSquared / Math.Pow(2, 2));

                    // Calculate 1%/1mm global
                    gs[4] = Math.Min(gs[4], Math.Pow((p.Value - d.Value) / (maxDose * 0.01), 2) +
                        (d.Position - position).LengthSquared / Math.Pow(1, 2));

                    // Calculate 1%/1mm local
                    gs[5] = Math.Min(gs[5], Math.Pow((p.Value - d.Value) / (d.Value * 0.01), 2) +
                        (d.Position - position).LengthSquared / Math.Pow(1, 2));
                }

                // Extract dose profile along z axis
                start = context.Image.UserToDicom(d.Position + new VVector(0, -2, 0), context.PlanSetup);
                end = context.Image.UserToDicom(d.Position + new VVector(0, 2, 0), context.PlanSetup);
                tps = context.PlanSetup.Dose.GetDoseProfile(start, end, new double[41]);

                // Loop through profile, converting the coordinates back from DICOM and calculating gamma/difference
                foreach (ProfilePoint p in tps)
                {
                    VVector position = context.Image.DicomToUser(p.Position, context.PlanSetup);

                    // Calculate 3%/3mm global
                    gs[0] = Math.Min(gs[0], Math.Pow((p.Value - d.Value) / (maxDose * 0.03), 2) +
                        (d.Position - position).LengthSquared / Math.Pow(3, 2));

                    // Calculate 3%/3mm local
                    gs[1] = Math.Min(gs[1], Math.Pow((p.Value - d.Value) / (d.Value * 0.03), 2) +
                        (d.Position - position).LengthSquared / Math.Pow(3, 2));

                    // Calculate 2%/2mm global
                    gs[2] = Math.Min(gs[2], Math.Pow((p.Value - d.Value) / (maxDose * 0.02), 2) +
                        (d.Position - position).LengthSquared / Math.Pow(2, 2));

                    // Calculate 2%/2mm local
                    gs[3] = Math.Min(gs[3], Math.Pow((p.Value - d.Value) / (d.Value * 0.02), 2) +
                        (d.Position - position).LengthSquared / Math.Pow(2, 2));

                    // Calculate 1%/1mm global
                    gs[4] = Math.Min(gs[4], Math.Pow((p.Value - d.Value) / (maxDose * 0.01), 2) +
                        (d.Position - position).LengthSquared / Math.Pow(1, 2));

                    // Calculate 1%/1mm local
                    gs[5] = Math.Min(gs[5], Math.Pow((p.Value - d.Value) / (d.Value * 0.01), 2) +
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

            // Sort differences
            differences.Sort();

            // Display results
            MessageBox.Show("Normalized TPS Dose Difference: " + (Math.Round(normDiff * 1000) / 10).ToString() + "%\n"
                + "Median TPS Dose Difference: " + (Math.Round(differences[differences.Count / 2] * 1000) / 10).ToString() + "%\n"
                + "Gamma Pass Rates (" + (threshold * 100).ToString()  + "% threshold): \n"
                + "     3%/3mm Global: " + (Math.Round(passingDetectors[0] / aboveThreshold * 1000) / 10).ToString() + "%\n"
                + "     3%/3mm Local: " + (Math.Round(passingDetectors[1] / aboveThreshold * 1000) / 10).ToString() + "%\n"
                + "     2%/2mm Global: " + (Math.Round(passingDetectors[2] / aboveThreshold * 1000) / 10).ToString() + "%\n"
                + "     2%/2mm Local: " + (Math.Round(passingDetectors[3] / aboveThreshold * 1000) / 10).ToString() + "%\n"
                + "     1%/1mm Global: " + (Math.Round(passingDetectors[4] / aboveThreshold * 1000) / 10).ToString() + "%\n"
                + "     1%/1mm Local: " + (Math.Round(passingDetectors[5] / aboveThreshold * 1000) / 10).ToString() + "%", 
                "MapCHECK Comparison Results");
        }
    }
}
