# ESAPI MapCHECK comparison plugin

by Mark Geurts <mark.w.geurts@gmail.com>
<br>Copyright &copy; 2025, Aspirus Health

## Description

`CompareMapcheck.easpi.dll` is a standalone ESAPI plugin that allows users to load Sun Nuclear MapCHECK (.txt) measurements and compare to the calculated dose of the selected plan using a gamma evaluation. This tool allows users to quickly evaluate the accuracy of their treatment planning system without the need to export each dose volume to DICOM. This type of evaluation is recommended as part of the validation of treatment planning system algorithms during commissioning in AAPM MPPG 5: 

Geurts MW, Jacqmin DJ, Jones LE, Kry SF, Mihailidis DN, Ohrt JD, Ritter T, Smilowitz JB, Wingreen NE. [AAPM MEDICAL PHYSICS PRACTICE GUIDELINE 5.b: Commissioning and QA of treatment planning dose calculations-Megavoltage photon and electron beams](https://doi.org/10.1002/acm2.13641). J Appl Clin Med Phys. 2022 Sep;23(9):e13641. doi: 10.1002/acm2.13641. Epub 2022 Aug 10. PMID: 35950259; PMCID: PMC9512346.

## Installation

To install this plugin, download a release and copy the .dll into the `PublishedScripts` folder of the Varian file server, then if required, register the script under Script Approvals in Eclipse. Alternatively, download the code from this repository and compile it yourself using Visual Studio.

## Usage and Documentation

1. Open a non-clinical patient plan in External Beam Planning that contains dose calculated on a water phantom.
2. Set the User Origin of the plan to the position of the central detector of the MapCHECK. The tool will extract a coronal plane dose at this depth centered on the origin.
3. Select Tools > Scripts, then choose CompareMapcheck.easpi.dll from the list.
4. When prompted, select the MapCHECK .txt file.
6. The results will be displayed on the user interface.
 
## License

Released under the GNU GPL v3.0 License for evaluating and testing purposes only. This tool should NOT be used to evaluate clinical plans or make decisions that impact patient care. See the [LICENSE](LICENSE) file for further details.
