using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace GitWrap {
    class Program {
        private static int outputLines = 0;

        static int Main(string[] args) {
            List<string> argList = new List<string> {
                "git"
            };

            // convert paths for bash
            foreach (string arg in args) {
                if (Directory.Exists(arg) || File.Exists(arg)) {
                    string lnxPath = ToLinuxPath(arg);
                    if (!String.IsNullOrEmpty(lnxPath)) {
                        argList.Add(lnxPath);
                    } else {
                        argList.Add(arg);
                    }
                } else {
                    argList.Add(arg);
                }
            }

            List<string> output = ExecuteBashWithArgs(GetBashPath(), argList);
            int exitCode = -1;
            if (int.TryParse(output[0], out exitCode))
                output.RemoveAt(0);

            // convert paths for windows
            foreach (string line in output) {
                string lineOut = "";
                string[] nullSegments = line.Split('\0');
                for (int nullSegment = 0; nullSegment < nullSegments.Length; nullSegment++) {
                    List<string> nullMatches = MatchesLinuxFileOrFolder(nullSegments[nullSegment]);
                    if (nullMatches != null) {
                        string winPath = ToWinPath(nullMatches[0]);
                        if (!String.IsNullOrEmpty(winPath)) {
                            lineOut += winPath;
                        } else {
                            lineOut += nullMatches[0];
                        }
                    } else {
                        string[] spaceSegments = nullSegments[nullSegment].Split(' ');
                        for (int spaceSegment = 0; spaceSegment < spaceSegments.Length; spaceSegment++) {
                            List<string> spaceMatches = MatchesLinuxFileOrFolder(spaceSegments[spaceSegment]);
                            if (spaceMatches != null) {
                                string longestMatch = spaceSegments[spaceSegment];
                                for (int nextSegment = spaceSegment + 1; nextSegment < spaceSegments.Length; nextSegment++) {
                                    string longerSearch = longestMatch + ' ' + spaceSegments[nextSegment];
                                    spaceMatches = MatchesLinuxFileOrFolder(longerSearch);
                                    if (spaceMatches != null)
                                        longestMatch = longerSearch;
                                }
                                string winPath = ToWinPath(longestMatch);
                                if (!String.IsNullOrEmpty(winPath)) {
                                    lineOut += winPath;
                                } else {
                                    lineOut += longestMatch;
                                }
                            } else {
                                lineOut += spaceSegments[spaceSegment];
                            }
                            if (spaceSegment + 1 < spaceSegments.Length)
                                lineOut += ' ';
                        }
                    }
                    if (nullSegment + 1 < nullSegments.Length)
                        lineOut += '\0';
                }
                PrintOutputData(lineOut);
            }
            return exitCode;
        }

        static List<string> MatchesLinuxFileOrFolder(string file) {
            if (String.IsNullOrEmpty(file))
                return null;

            List<string> args = new List<string> {
                "ls -QAd \\\"" + file + "\\\"*"
            };
            List<string> output = ExecuteBashWithArgs(GetBashPath(), args);

            int exitCode = -1;
            if (!int.TryParse(output[0], out exitCode) || exitCode != 0)
                return null;
            output.RemoveAt(0);

            List<string> matches = new List<string>();
            foreach (string line in output) {
                string[] segments = line.Split('"');
                foreach (string segment in segments) {
                    if (!String.IsNullOrWhiteSpace(segment))
                        matches.Add(segment);
                }
            }

            return matches;
        }

        static string ToLinuxPath(string winPath) {
            List<string> args = new List<string> {
                "wslpath -u \\\"" + winPath + "\\\""
            };
            List<string> output = ExecuteBashWithArgs(GetBashPath(), args);

            int exitCode = -1;
            if (!int.TryParse(output[0], out exitCode) || exitCode != 0)
                return null;

            return output[1];
        }

        static string ToWinPath(string linuxPath) {
            List<string> args = new List<string>{
                "wslpath -w \\\"" + linuxPath + "\\\""
            };
            List<string> output = ExecuteBashWithArgs(GetBashPath(), args);

            int exitCode = -1;
            if (!int.TryParse(output[0], out exitCode) || exitCode != 0)
                return null;

            return output[1];
        }

        static List<string> ExecuteBashWithArgs(String bashPath, List<string> args) {
            List<string> output = new List<string>();
            if (!File.Exists(bashPath)) {
                output.Add("[-] Error: Bash.exe not found.");
                output.Insert(0, "-1");
                return output;
            }

            ProcessStartInfo bashInfo = new ProcessStartInfo {
                FileName = bashPath
            };

            // Loop through args and pass them to bash executable
            String argsString = "-c \"";
            foreach (string arg in args)
                argsString += " " + arg;
            argsString += "\"";

            bashInfo.Arguments = argsString;
            bashInfo.UseShellExecute = false;
            bashInfo.RedirectStandardOutput = true;
            bashInfo.RedirectStandardError = true;
            bashInfo.CreateNoWindow = true;

            var proc = new Process {
                StartInfo = bashInfo
            };

            proc.OutputDataReceived += new DataReceivedEventHandler((sender, e) => {
                if (!String.IsNullOrEmpty(e.Data))
                    output.Add(e.Data);
            });
            proc.ErrorDataReceived += new DataReceivedEventHandler((sender, e) => {
                if (!String.IsNullOrEmpty(e.Data))
                    output.Add(e.Data);
            });

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            proc.WaitForExit();

            output.Insert(0, proc.ExitCode.ToString());
            return output;
        }

        static String GetBashPath() {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            @"System32\bash.exe");
        }

        static void PrintOutputData(String data) {
            if (data != null) {
                if (outputLines > 0) {
                    Console.Write(Environment.NewLine);
                }
                Console.Write(data);
                outputLines++;
            }
        }
    }
}
