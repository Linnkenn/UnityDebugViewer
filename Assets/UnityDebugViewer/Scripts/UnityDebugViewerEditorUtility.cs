﻿using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditorInternal;

namespace UnityDebugViewer
{
    public static class UnityDebugViewerEditorUtility
    {
        public const char UnityInternalDirectorySeparator = '/';
        public const string EllipsisStr = "........";
        public const int DisplayLineNumber = 8;

        public static bool JumpToSource(LogData log)
        {
            if(log != null)
            {
                for (int i = 0; i < log.stackList.Count; i++)
                {
                    var stack = log.stackList[i];
                    if (stack == null)
                    {
                        continue;
                    }

                    if (JumpToSource(stack))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool JumpToSource(LogStackData stack)
        {
            if(stack == null)
            {
                return false;
            }
            else
            {
                return JumpToSource(stack.filePath, stack.lineNumber);
            }
        }

        public static bool JumpToSource(string filePath, int lineNumber)
        {
            var validFilePath = ConvertToSystemFilePath(filePath);
            if (File.Exists(validFilePath))
            {
                if(InternalEditorUtility.OpenFileAtLineExternal(validFilePath, lineNumber))
                {
                    return true;
                }
            }

            return false;
        }

        public static string GetSourceContent(string filePath, int lineNumber)
        {
            var validFilePath = ConvertToSystemFilePath(filePath);
            if (!File.Exists(validFilePath))
            {
                return string.Empty;
            }

            var lineArray = File.ReadAllLines(validFilePath);

            int fileLineNumber = lineNumber - 1;
            int firstLine = Mathf.Max(fileLineNumber - DisplayLineNumber / 2, 0);
            int lastLine = Mathf.Min(fileLineNumber + DisplayLineNumber / 2 + 1, lineArray.Count());

            string souceContent = string.Empty;
            if(firstLine != 0)
            {
                souceContent = string.Format("{0}\n{1}", EllipsisStr, souceContent);
            }
            for(int index = firstLine;index < lastLine;index++)
            {
                string str = ReplaceTabWithSpace(lineArray[index]) + "\n";
                if(index == fileLineNumber)
                {
                    str = string.Format("<color=#ff0000ff>{0}</color>", str);
                }

                souceContent += str;
            }
            if(lastLine != lineArray.Count())
            {
                souceContent = string.Format("{0}\n{1}", souceContent, EllipsisStr);
            }

            return souceContent;
        }

        /// <summary>
        /// convert the format of the incoming file path to the format of system file path, and complete the incoming file path if necessary
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static string ConvertToSystemFilePath(string filePath)
        {
            string systemFilePath = filePath.Replace(UnityInternalDirectorySeparator, Path.DirectorySeparatorChar);

            /// Only complete the file path of the log generated by this project 
            if (systemFilePath.StartsWith("Assets" + Path.DirectorySeparatorChar))
            {
                systemFilePath = Path.Combine(Directory.GetCurrentDirectory(), systemFilePath);
            }
            return systemFilePath;
        }

        public static string ConvertToUnityFilePath(string filePath)
        {
            string startStr = "Assets" + UnityInternalDirectorySeparator;
            filePath = filePath.Replace(Path.DirectorySeparatorChar, UnityInternalDirectorySeparator);
            if(filePath.StartsWith(startStr) == false)
            {
                int index = filePath.IndexOf(startStr);
                if(index == -1)
                {
                    return string.Empty;
                }
                else
                {
                    return filePath.Substring(index);
                }
            }
            else
            {
                return filePath;
            }
        }

        /// <summary>
        /// replace \t with four \b to ensure consistent code format
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private static string ReplaceTabWithSpace(string str)
        {
            return str.Replace("\t", "\b\b\b\b");
        }
    }
}