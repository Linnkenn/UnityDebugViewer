﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace UnityDebugViewer
{
    [Serializable]
    public struct LogFilter
    {
        public bool collapse;
        public bool showLog;
        public bool showWarning;
        public bool showError;
        public string searchText;

        public bool Equals(LogFilter filter)
        {
            return this.collapse == filter.collapse && this.showLog == filter.showLog && this.showWarning == filter.showWarning && this.showError == filter.showError && this.searchText.Equals(filter.searchText);
        }

        public bool ShouldDisplay(LogData log)
        {
            bool canDisplayInType;
            switch (log.type)
            {
                case LogType.Log:
                    canDisplayInType = this.showLog;
                    break;

                case LogType.Warning:
                    canDisplayInType = this.showWarning;
                    break;
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    canDisplayInType = this.showError;
                    break;
                default:
                    canDisplayInType = false;
                    break;
            }

            if (canDisplayInType)
            {
                if (string.IsNullOrEmpty(searchText))
                {
                    return true;
                }
                else
                {
                    if(Regex.IsMatch(log.info, searchText))
                    {
                        return true;
                    }
                    else
                    {
                        /// Lowercase and try again
                        return Regex.IsMatch(log.info.ToLower(), searchText.ToLower());
                    }
                }
            }
            else
            {
                return false;
            }
        }
    }

    /// <summary>
    /// The backend of UnityDebugViewer and provide data for UnityDebugViewerWindow
    /// </summary>
    [Serializable]
    public class UnityDebugViewerEditor : ScriptableObject, ISerializationCallbackReceiver
    {
        #region 用于保存log数据
        /// <summary>
        /// log显示的最大条数
        /// </summary>
        public const int MAX_DISPLAY_NUM = 999;

        protected int _logNum = 0;
        public int logNum
        {
            get
            {
                return _logNum;
            }
            private set
            {
                int num = value > MAX_DISPLAY_NUM ? MAX_DISPLAY_NUM : value;
                _logNum = num;
            }
        }
        protected int _warningNum = 0;
        public int warningNum
        {
            get
            {
                return _warningNum;
            }
            private set
            {
                int num = value > MAX_DISPLAY_NUM ? MAX_DISPLAY_NUM : value;
                _warningNum = num;
            }
        }
        protected int _errorNum = 0;
        public int errorNum
        {
            get
            {
                return _errorNum;
            }
            private set
            {
                int num = value > MAX_DISPLAY_NUM ? MAX_DISPLAY_NUM : value;
                _errorNum = num;
            }
        }

        protected List<LogData> _logList = null;
        private List<LogData> logList
        {
            get
            {
                if (_logList == null)
                {
                    _logList = new List<LogData>();
                }

                return _logList;
            }
        }

        protected List<LogData> _collapsedLogList = null;
        private List<LogData> collapsedLogList
        {
            get
            {
                if (_collapsedLogList == null)
                {
                    _collapsedLogList = new List<LogData>();
                }

                return _collapsedLogList;
            }
        }

        /// <summary>
        /// Dictionary序列化时会丢失数据，需要手动序列化
        /// </summary>
        protected Dictionary<string, CollapsedLogData> _collapsedLogDataDic = null;
        protected Dictionary<string, CollapsedLogData> collapsedLogDic
        {
            get
            {
                if (_collapsedLogDataDic == null)
                {
                    _collapsedLogDataDic = new Dictionary<string, CollapsedLogData>();
                }

                return _collapsedLogDataDic;
            }
        }
        [SerializeField]
        private List<string> serializeKeyList = new List<string>();
        [SerializeField]
        private List<CollapsedLogData> serializeValueList = new List<CollapsedLogData>();


        /// <summary>
        /// the log list used to display by UnityDebugViewerWindow
        /// </summary>
        private List<LogData> _filteredLogList = null;
        private List<LogData> filteredLogList
        {
            get
            {
                if(_filteredLogList == null)
                {
                    _filteredLogList = new List<LogData>();
                }

                return _filteredLogList;
            }
        }
        [SerializeField]
        private LogFilter logFilter;


        [SerializeField]
        public LogData selectedLog = null;
        #endregion

        private UnityDebugViewerEditorType _type = UnityDebugViewerEditorType.Editor;
        public UnityDebugViewerEditorType type
        {
            get
            {
                return _type;
            }
        }

        public static UnityDebugViewerEditor CreateInstance(UnityDebugViewerEditorType editorType)
        {
            var editor = ScriptableObject.CreateInstance<UnityDebugViewerEditor>();

            BindingFlags flag = BindingFlags.Instance | BindingFlags.NonPublic;
            Type type = editor.GetType();
            FieldInfo field = type.GetField("_type", flag);
            field.SetValue(editor, editorType);
            return editor;
        }

        /// <summary>
        /// 序列化结束时会被调用
        /// </summary>
        private void OnEnable()
        {
            /// 确保在序列化时，可序列化的数据成员不会被重置
            hideFlags = HideFlags.HideAndDontSave;
        }

        public void ClearLog()
        {
            List<LogData> interErrorLogList = new List<LogData>();
            for (int i = 0; i < logList.Count; i++)
            {
                var log = logList[i];
                if (log == null || log.type == LogType.Log || log.type == LogType.Warning)
                {
                    continue;
                }

                if (log.isCompilingLog)
                {
                    interErrorLogList.Add(log);
                }
            }

            logList.Clear();
            collapsedLogList.Clear();
            collapsedLogDic.Clear();
            filteredLogList.Clear();

            selectedLog = null;
            logNum = 0;
            warningNum = 0;
            errorNum = 0;

            for(int i = 0; i < interErrorLogList.Count; i++)
            {
                AddLog(interErrorLogList[i]);
            }
        }

        public int GetLogNum(LogData data)
        {
            int num = 1;
            string key = data.GetKey();
            if (collapsedLogDic.ContainsKey(key))
            {
                num = collapsedLogDic[key].count;
            }

            return num;
        }

        public List<LogData> GetFilteredLogList(LogFilter filter, bool forceUpdate = false)
        {
            if (forceUpdate || !this.logFilter.Equals(filter))
            {
                this.filteredLogList.Clear();

                var logList = filter.collapse ? this.collapsedLogList : this.logList;
                for(int i = 0; i < logList.Count; i++)
                {
                    var log = logList[i];
                    if(log == null)
                    {
                        continue;
                    }

                    if (filter.ShouldDisplay(log))
                    {
                        this.filteredLogList.Add(log);
                    }
                }

                this.logFilter = filter;
            }

            return filteredLogList;
        }

        /// <summary>
        /// reset all log caused by compilation
        /// </summary>
        public void ResetCompilingLog()
        {
            for (int i = 0; i < logList.Count; i++)
            {
                var log = logList[i];
                if (log == null)
                {
                    continue;
                }

                if (log.isCompilingLog)
                {
                    log.ResetCompilingState();
                }
            }
        }

        public void AddLog(LogData data)
        {
            switch (data.type)
            {
                case LogType.Log:
                    logNum++;
                    break;
                case LogType.Warning:
                    warningNum++;
                    break;
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    errorNum++;
                    break;
            }
            logList.Add(data);

            /// add collapsed log data
            CollapsedLogData collapsedLogData;
            string key = data.GetKey();
            var cloneLog = data.Clone();
            if (collapsedLogDic.ContainsKey(key))
            {
                collapsedLogData.count = collapsedLogDic[key].count + 1;
                collapsedLogData.log = collapsedLogDic[key].log;
                collapsedLogDic[key] = collapsedLogData;
            }
            else
            {
                collapsedLogData.log = cloneLog;
                collapsedLogData.count = 1;
                collapsedLogDic.Add(key, collapsedLogData);
                collapsedLogList.Add(cloneLog);
            }

            if (logFilter.ShouldDisplay(data))
            {
                filteredLogList.Add(data);
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            this.serializeKeyList.Clear();
            this.serializeValueList.Clear();

            foreach (var pair in this.collapsedLogDic)
            {
                this.serializeKeyList.Add(pair.Key);
                this.serializeValueList.Add(pair.Value);
            }
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            int count = Mathf.Min(this.serializeKeyList.Count, this.serializeValueList.Count);
            for (int i = 0; i < count; ++i)
            {
                this.collapsedLogDic.Add(this.serializeKeyList[i], this.serializeValueList[i]);
            }
        }
    }
}