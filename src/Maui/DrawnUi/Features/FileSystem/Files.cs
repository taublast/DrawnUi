using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrawnUi.Infrastructure
{
    public partial class Files
    {


        #region Resources



        #endregion

        public static bool PermissionsOk { get; set; }

        public static async void CheckPermissionsAsync(Action onSuccess = null)
        {

            void Success()
            {
                Files.PermissionsOk = true;

                if (onSuccess != null)
                    onSuccess();
            }

#if WINDOWS
            // Windows

#elif ANDROID || IOS || MACCATALYST


            var status = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
            if (status != PermissionStatus.Granted)
            {
                Files.PermissionsOk = false;

                Tasks.StartTimerAsync(TimeSpan.FromMilliseconds(1000), async () =>
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        status = await Permissions.RequestAsync<Permissions.StorageWrite>();
                        if (status == PermissionStatus.Granted)
                        {
                            Success();
                        }
                    });
                    return false;
                });
                return;
            }

#elif MACCATALYST
// MacCatalyst

#endif

            Success();
        }

        public static string GetFullFilename(string filename, StorageType type, string subFolder = null)
        {
            var path = FileSystem.Current.CacheDirectory;

            //path = FileSystem.Current.AppDataDirectory;
            //if (type == StorageType.Public)
            //      {
            //          path = GetPublicDirectory();
            //      }
            //      else
            //      if (type == StorageType.Internal)
            //      {
            //          path = FileSystem.Current.AppDataDirectory;
            //      }

            if (!string.IsNullOrEmpty(subFolder))
            {
                path = Path.Combine(path, subFolder);
            }

            string targetFile = Path.Combine(path, filename);

            return targetFile;
        }

        public static FileDescriptor OpenFile(string filename, StorageType type, string subFolder = null)
        {
            var path = FileSystem.Current.CacheDirectory;


            if (!string.IsNullOrEmpty(subFolder))
            {
                path = Path.Combine(path, subFolder);

                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
            }

            string targetFile = Path.Combine(path, filename);

            var file = new FileDescriptor
            {
                Filename = filename,
                Path = path,
                FullFilename = targetFile
            };

            file.Handler = new FileStream(targetFile, System.IO.FileMode.OpenOrCreate);

            return file;
        }

        public static bool CloseFile(FileDescriptor file, bool refreshSystem = false)
        {
            file.Dispose();

#if ONPLATFORM
            if (refreshSystem)
            {
                RefreshSystem(file);
            }
#endif
            return true;
        }

        //public static async Task WriteFile(FileDescriptor file, Stream stream)
        //{

        //}

    }
}
