﻿using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

#if UNITY_EDITOR_LINUX
using UnityEditor;
#endif

namespace LDtkUnity.Editor
{
    internal static class LDtkTilesetExporterUtil
    {
        private const string EXPORT_ZIP = "LDtkTilesetExporter.zip";
        private const string EXPORT_APP = "ExportTilesetDefinition.exe";
        private const string MAC_APP = "ExportTilesetDefinitionMac.sh";
        private const string LINUX_APP = "ExportTilesetDefinitionLinux.sh";

        //[MenuItem("UnzipToProject/Unzip")]
        public static void UnzipToLibrary()
        {
            string pathToZip = PathToZip();
            string destDir = PathToLibraryDir();

            LDtkPathUtility.TryCreateDirectory(destDir);
            if (Directory.Exists(destDir))
            {
                DirectoryInfo di = new DirectoryInfo(destDir);
                foreach (FileInfo file in di.GetFiles())
                {
                    file.Delete();
                }
            }

            Debug.Assert(File.Exists(pathToZip), "File.Exists(pathToZip)");
            Debug.Assert(Directory.Exists(destDir), "Directory.Exists(destDir)");

            ZipUtil.Extract(pathToZip, destDir);

#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            string pathToExe = PathToExe();

            //if mac/Linux, we need to create a shell script to run the exe
#if UNITY_EDITOR_OSX
            string shPath = Path.Combine(destDir, MAC_APP);
            string shContent = $"#!/bin/sh\n /Library/Frameworks/Mono.framework/Versions/Current/Commands/mono {pathToExe} $1";
#elif UNITY_EDITOR_LINUX
            string shPath = Path.Combine(destDir, LINUX_APP);
            var unityPath = Path.GetDirectoryName(EditorApplication.applicationPath);
            var monoPath = Path.Combine(unityPath, "Data", "MonoBleedingEdge", "bin", "mono");
            string shContent = $"#!/bin/sh\n {monoPath} {pathToExe} $1";
#endif

            File.WriteAllText(shPath, shContent);

            //on mac/Linux, the app needs some permission. Use "sudo chmod +x"
            Process.Start("/bin/bash", $"-c \" chmod +x  {shPath}\" ");
#endif

            LDtkDebug.Log($"Extracted the tileset export app to \"{destDir}\"");
        }

        /*//[MenuItem("UnzipToProject/AppVersion")]
        private static void CheckAppVersion()
        {
            Debug.Log($"app version up to date? {GetAppUpToDate()}");
        }

        //[MenuItem("UnzipToProject/LogPathToExe")]
        public static void LogPathToExe()
        {
            Debug.Log(PathToExe());
        }*/

        public static string PathToLibraryDir()
        {
            string destDir = Application.dataPath;
            destDir = Path.Combine(destDir, "..", "Library", "LDtkTilesetExporter");
            destDir = Path.GetFullPath(destDir);
            return destDir;
        }

        public static string PathToZip()
        {
            string packagePath = Path.Combine(LDtkInternalUtility.ASSETS, EXPORT_ZIP);
            if (File.Exists(packagePath)) return packagePath;

            string assetsPath = Path.Combine(LDtkInternalUtility.PACKAGES, EXPORT_ZIP);
            if (File.Exists(assetsPath)) return assetsPath;

            return null;
        }
        public static string PathToExe()
        {
            string exePath = Path.Combine(PathToLibraryDir(), EXPORT_APP);
            return exePath;
        }
        public static string PathToMacSh()
        {
            string exePath = Path.Combine(PathToLibraryDir(), MAC_APP);
            return exePath;
        }

        public static string PathToLinuxSh()
        {
            string exePath = Path.Combine(PathToLibraryDir(), LINUX_APP);
            return exePath;
        }

        public static bool GetAppUpToDate(out Version version, out Version requiredVersion)
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(PathToExe());
            version = new Version(info.FileVersion);
            requiredVersion = new Version(LDtkImporterConsts.EXPORT_APP_VERSION_REQUIRED);
            //Debug.Log($"app version {version}, required {requiredVersion}");
            return version == requiredVersion;
        }
    }
}
