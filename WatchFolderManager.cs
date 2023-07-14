#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2023 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using ShareX.HelpersLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ShareX
{
    /// <summary>
    /// The <see cref="WatchFolderManager"/> class is responsible for managing the functionality related to watching folders
    /// for changes and performing actions based on those changes.
    /// </summary>
    public class WatchFolderManager : IDisposable
    {
        /// <summary>
        /// The list of <see cref="WatchFolder"/> objects associcated with the <see cref="WatchFolderManager"></see>
        /// </summary>
        public List<WatchFolder> WatchFolders { get; private set; }

        /// <summary>
        /// Updates the list of watch folders based on the <see cref="WatchFolderSettings"/>
        /// and <see cref="HotkeySettings"/> defined in the application.
        /// It clears the existing watch folders and creates new instances of <see cref="WatchFolder"/> for each watch folder setting.
        /// </summary>
        public void UpdateWatchFolders()
        {
            if (WatchFolders != null)
            {
                UnregisterAllWatchFolders();
            }

            WatchFolders = new List<WatchFolder>();

            foreach (WatchFolderSettings defaultWatchFolderSetting in Program.DefaultTaskSettings.WatchFolderList)
            {
                AddWatchFolder(defaultWatchFolderSetting, Program.DefaultTaskSettings);
            }

            foreach (HotkeySettings hotkeySetting in Program.HotkeysConfig.Hotkeys)
            {
                foreach (WatchFolderSettings watchFolderSetting in hotkeySetting.TaskSettings.WatchFolderList)
                {
                    AddWatchFolder(watchFolderSetting, hotkeySetting.TaskSettings);
                }
            }
        }

        /// <summary>
        /// Searches for a watch folder in the <see cref="WatchFolders"/> list
        /// based on the provided <paramref name="watchFolderSetting"/>.
        /// </summary>
        /// <param name="watchFolderSetting"></param>
        /// If no matching watch folder is found, it returns `null`. 
        /// <returns>The first matching <see cref="WatchFolder"/> object;
        /// otherwise it returns <see langword="null"/> </returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        private WatchFolder FindWatchFolder(WatchFolderSettings watchFolderSetting)
        {
            return WatchFolders.FirstOrDefault(watchFolder => watchFolder.Settings == watchFolderSetting);
        }

        /// <summary>
        /// Checks if a watch folder exists in the <see cref="WatchFolders"/> list
        /// based on the provided <paramref name="watchFolderSetting"/>. 
        /// </summary>
        /// <param name="watchFolderSetting"></param>
        /// <returns> It returns <see langword="true"/> if the watch folder exists,
        /// and <see langword="false"/> otherwise</returns>
        private bool IsExist(WatchFolderSettings watchFolderSetting)
        {
            return FindWatchFolder(watchFolderSetting) != null;
        }

        /// <summary>
        /// Adds a new watch folder to the <see cref="WatchFolders"/> list.
        /// It creates a new <see cref="WatchFolder"/> instance, associates it with the provided <paramref name="watchFolderSetting"/> and <paramref name="taskSettings"/>, and adds it to the list.
        /// If the <paramref name="watchFolderSetting"/> doesn't exist in the <see cref="TaskSettings.WatchFolderList"/> in <paramref name="taskSettings"/>, it adds it to the list.
        /// If the <see cref="TaskSettings.WatchFolderEnabled"/> is <see langword="true"/>, it enables the watch folder.
        /// </summary>
        /// <param name="watchFolderSetting"></param>
        /// <param name="taskSettings"></param>
        public void AddWatchFolder(WatchFolderSettings watchFolderSetting, TaskSettings taskSettings)
        {
            if (!IsExist(watchFolderSetting))
            {
                if (!taskSettings.WatchFolderList.Contains(watchFolderSetting))
                {
                    taskSettings.WatchFolderList.Add(watchFolderSetting);
                }

                WatchFolder watchFolder = new WatchFolder();
                watchFolder.Settings = watchFolderSetting;
                watchFolder.TaskSettings = taskSettings;

                watchFolder.FileWatcherTrigger += origPath =>
                {
                    TaskSettings taskSettingsCopy = TaskSettings.GetSafeTaskSettings(taskSettings);
                    string destPath = origPath;

                    if (watchFolderSetting.MoveFilesToScreenshotsFolder)
                    {
                        string screenshotsFolder = TaskHelpers.GetScreenshotsFolder(taskSettingsCopy);
                        string fileName = Path.GetFileName(origPath);
                        destPath = TaskHelpers.HandleExistsFile(screenshotsFolder, fileName, taskSettingsCopy);
                        FileHelpers.CreateDirectoryFromFilePath(destPath);
                        File.Move(origPath, destPath);
                    }

                    UploadManager.UploadFile(destPath, taskSettingsCopy);
                };

                WatchFolders.Add(watchFolder);

                if (taskSettings.WatchFolderEnabled)
                {
                    watchFolder.Enable();
                }
            }
        }
        /// <summary>
        /// Removes a watch folder from the <see cref="WatchFolders"/> list based on the provided <paramref name="watchFolderSetting"/>.
        /// It removes the corresponding watch folder from the <see cref="TaskSettings.WatchFolderList"/> and disposes of the <see cref="WatchFolder"/> object. 
        /// </summary>
        /// <param name="watchFolderSetting"></param>
        public void RemoveWatchFolder(WatchFolderSettings watchFolderSetting)
        {
            using (WatchFolder watchFolder = FindWatchFolder(watchFolderSetting))
            {
                if (watchFolder != null)
                {
                    watchFolder.TaskSettings.WatchFolderList.Remove(watchFolderSetting);
                    WatchFolders.Remove(watchFolder);
                }
            }
        }

        /// <summary>
        /// Updates the state of a watch folder in the <see cref="WatchFolders"/> list based on the provided <paramref name="watchFolderSetting"/>.
        /// If <see cref="TaskSettings.WatchFolderEnabled"/> is <see langword="true"/> for the <see cref="WatchFolder"/>, it enables the watch folder.
        /// Otherwise, it disposes of the <see cref="WatchFolder"/> object. 
        /// </summary>
        /// <param name="watchFolderSetting"></param>
        public void UpdateWatchFolderState(WatchFolderSettings watchFolderSetting)
        {
            WatchFolder watchFolder = FindWatchFolder(watchFolderSetting);
            if (watchFolder != null)
            {
                if (watchFolder.TaskSettings.WatchFolderEnabled)
                {
                    watchFolder.Enable();
                }
                else
                {
                    watchFolder.Dispose();
                }
            }
        }

        /// <summary>
        /// Unregisters all the watch folders in the <see cref="WatchFolders"/> list by disposing of each <see cref="WatchFolder"/> object.
        /// </summary>
        public void UnregisterAllWatchFolders()
        {
            if (WatchFolders != null)
            {
                foreach (WatchFolder watchFolder in WatchFolders)
                {
                    if (watchFolder != null)
                    {
                        watchFolder.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Disposes of all the watch folders in the <see cref="WatchFolders"/> list by calling the <see cref="UnregisterAllWatchFolders"/> method.
        /// </summary>
        public void Dispose()
        {
            UnregisterAllWatchFolders();
        }
    }
}