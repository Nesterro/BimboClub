using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.Serialization;

namespace RevitServerManager.Models
{
    [DataContract]
    public class ServerProperties
    {
        [DataMember(Name = "AccessLevels")]
        public List<string> AccessLevels { get; set; } = new();

        [DataMember(Name = "CentralServerVersion")]
        public string CentralServerVersion { get; set; } = "";

        [DataMember(Name = "CurrentMaxRNum")]
        public long CurrentMaxRNum { get; set; }

        [DataMember(Name = "Freespace")]
        public long Freespace { get; set; }

        [DataMember(Name = "ServerName")]
        public string ServerName { get; set; } = "";

        [DataMember(Name = "ServerRoles")]
        public List<string> ServerRoles { get; set; } = new();

        [DataMember(Name = "ServerVersion")]
        public string ServerVersion { get; set; } = "";

        [DataMember(Name = "StorageCapacity")]
        public long StorageCapacity { get; set; }
    }

    [DataContract]
    public class FolderContents
    {
        [DataMember(Name = "DriveFreeSpace")]
        public long DriveFreeSpace { get; set; }

        [DataMember(Name = "DriveSpace")]
        public long DriveSpace { get; set; }

        [DataMember(Name = "Folders")]
        public List<FolderObjectData> Folders { get; set; } = new();

        [DataMember(Name = "Models")]
        public List<ModelObjectData> Models { get; set; } = new();

        [DataMember(Name = "Path")]
        public string Path { get; set; } = "";
    }

    [DataContract]
    public class FolderObjectData
    {
        [DataMember(Name = "HasContents")]
        public bool HasContents { get; set; }

        [DataMember(Name = "LockContext")]
        public int LockContext { get; set; }

        [DataMember(Name = "LockState")]
        public int LockState { get; set; }

        [DataMember(Name = "ModelCount")]
        public int ModelCount { get; set; }

        [DataMember(Name = "FolderCount")]
        public int FolderCount { get; set; }

        [DataMember(Name = "Name")]
        public string Name { get; set; } = "";

        [DataMember(Name = "Size")]
        public long Size { get; set; }
    }

    [DataContract]
    public class ModelObjectData
    {
        [DataMember(Name = "LockContext")]
        public int LockContext { get; set; }

        [DataMember(Name = "LockState")]
        public int LockState { get; set; }

        [DataMember(Name = "ModelOption")]
        public int ModelOption { get; set; }

        [DataMember(Name = "Name")]
        public string Name { get; set; } = "";

        [DataMember(Name = "Size")]
        public long Size { get; set; }

        [DataMember(Name = "SupportSize")]
        public long SupportSize { get; set; }
    }

    public class ServerNodeViewModel : INotifyPropertyChanged
    {
        private bool _isOnline = true;
        private string _statusText = "Готов к подключению";

        public string ServerAddress { get; set; }
        public string Version { get; set; }
        public string DisplayName => ServerAddress;
        public bool IsLoaded { get; set; }

        public bool IsOnline
        {
            get => _isOnline;
            set
            {
                if (_isOnline != value)
                {
                    _isOnline = value;
                    OnPropertyChanged(nameof(IsOnline));
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public ObservableCollection<FolderViewModel?> SubFolders { get; }

        public ServerNodeViewModel(string serverAddress, string version)
        {
            ServerAddress = serverAddress;
            Version = version;
            SubFolders = new ObservableCollection<FolderViewModel?>
            {
                null // Dummy item for expander
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    public class FolderViewModel : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string ServerAddress { get; set; }
        public string ServerVersion { get; set; }
        public string ServerRelativePath { get; set; }
        public bool IsLoaded { get; set; }
        public ObservableCollection<FolderViewModel?> SubFolders { get; }

        public FolderViewModel(string name, string serverRelativePath, string serverAddress = "", string serverVersion = "", bool hasSubfolders = false)
        {
            Name = name;
            ServerRelativePath = serverRelativePath;
            ServerAddress = serverAddress;
            ServerVersion = serverVersion;
            SubFolders = new ObservableCollection<FolderViewModel?>();
            
            if (hasSubfolders)
            {
                // Dummy item to show expansion arrow
                SubFolders.Add(null);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    public class ModelFileViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        private string _status = "Готов";
        private int _progress = 0;

        public string Name { get; set; }
        public string FolderPath { get; set; }
        public string ServerAddress { get; set; }
        public string ServerVersion { get; set; }
        public long SizeBytes { get; set; }
        public string FormattedSize => FormatSize(SizeBytes);
        public string ServerRelativeModelPath => string.IsNullOrWhiteSpace(FolderPath) ? Name : $"{FolderPath}|{Name}";
        public string DisplayRsnPath => $"RSN://{ServerAddress}/{ServerRelativeModelPath.Replace('|', '/')}";

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        public int Progress
        {
            get => _progress;
            set
            {
                if (_progress != value)
                {
                    _progress = value;
                    OnPropertyChanged(nameof(Progress));
                }
            }
        }

        public ModelFileViewModel(string name, string folderPath, long sizeBytes, string serverAddress = "", string serverVersion = "")
        {
            Name = name;
            FolderPath = folderPath;
            SizeBytes = sizeBytes;
            ServerAddress = serverAddress;
            ServerVersion = serverVersion;
        }

        private static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "0 Б";
            string[] units = { "Б", "КБ", "МБ", "ГБ" };
            double size = bytes;
            int unitIdx = 0;
            while (size >= 1024 && unitIdx < units.Length - 1)
            {
                size /= 1024;
                unitIdx++;
            }
            return $"{size:F1} {units[unitIdx]}";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
