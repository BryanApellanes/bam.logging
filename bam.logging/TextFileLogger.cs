/*
	Copyright © Bryan Apellanes 2015  
*/
using System.IO;
using Bam.Configuration;
using Bam;

namespace Bam.Logging
{
    /// <summary>
    /// A logger used to log events to a text file
    /// </summary>
    public class TextFileLogger: Logger
    {
        int _fileNumber;

        public TextFileLogger(IApplicationNameProvider applicationNameProvider)
            : base()
        {
            ApplicationNameProvider = applicationNameProvider;
            _fileNumber = 1;
            MaxBytes = 1048576; // 1 MB
            FileExtension = "log";
            Folder = new DirectoryInfo(GetProfileDataFolder());            
        }


        protected IApplicationNameProvider ApplicationNameProvider { get; set; }

        /// <summary>
        /// Gets or sets the maximum size of any single log file created by this logger.
        /// No effect if XmlLogger, use MaxEntries instead.
        /// </summary>
        public long MaxBytes
        {
            get;
            set;
        }

        protected string FileExtension
        {
            get;
            set;
        }

        protected int FileNumber
        {
            get
            {
                return _fileNumber;
            }
            set
            {
                _fileNumber = value;
            }
        }

        DirectoryInfo _folder;
        object _folderLock = new object();
        /// <summary>
        /// Gets or sets the directory where logs are written.
        /// </summary>
        public DirectoryInfo Folder
        {
            get
            {
                return _folderLock.DoubleCheckLock(ref _folder, () => new DirectoryInfo(GetProfileDataFolder()));
            }
            set
            {
                _folder = value;
                if (!_folder.Exists)
                {
                    _folder.Create();
                }
                this._fileNumber = 1;
                this.SetNextFileInfo();
            }
        }

        FileInfo _file;
        /// <summary>
        /// Gets the FileInfo representing the current log.  This will change as the file reaches 
        /// the max size or entries per file for XmlLogger.
        /// </summary>
        public FileInfo File
        {
            get
            {
                return _file;
            }
        }

        /// <summary>
        /// Writes the specified logEvent to the file referenced by the File property
        /// of the current TextFileLogger.
        /// </summary>
        /// <param name="logEvent"></param>
        public override void CommitLogEvent(LogEvent logEvent)
        {
            string logText = GetLogText(logEvent);

            logText.SafeAppendToFile(this._file.FullName);

            SetNextFileInfoIfNecessary();
        }

        protected virtual string GetLogText(LogEvent logEvent)
        {
            string logText = string.Format("{0}\r\n****************\r\n\r\n", logEvent.PropertiesToString());
            return logText;
        }

        object fileLock = new object();
        protected virtual void SetNextFileInfoIfNecessary()
        {
            if (_file == null)
            {
                SetNextFileInfo();
            }
            else
            {
                lock (fileLock)
                {                    
                    _file.Refresh();                    
                }
                if (_file.Exists && _file.Length >= this.MaxBytes)
                {
                    SetNextFileInfo();
                }
            }
        }

        /// <summary>
        /// Increments the file number if the current file number already exists.
        /// </summary>
        protected void SetNextFileInfo()
        {
            lock (fileLock)
            {
                string appName = ApplicationNameProvider.GetApplicationName();
                string fileName = string.Format("{0}_{1}.{2}", appName, _fileNumber, FileExtension);

                _file = new FileInfo(Path.Combine(Folder.FullName, fileName));

                while (_file.Exists)
                {
                    _fileNumber += 1;
                    fileName = string.Format("{0}_{1}.{2}", appName, _fileNumber, FileExtension);
                    _file = new FileInfo(Path.Combine(Folder.FullName, fileName));
                }
            }
        }
        
        protected static string GetProfileDataFolder()
        {
            return BamProfile.DataPath;
        }
    }
}
