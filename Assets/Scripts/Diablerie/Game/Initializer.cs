using System;
using System.IO;
using Diablerie.Engine;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.IO.D2Formats;
using Diablerie.Game.UI;
using Diablerie.Game.UI.Menu;
using UnityEngine;

namespace Diablerie.Game
{
    public class Initializer : MonoBehaviour
    {
        public static string DataPath = null;

        private string MpqDir
        {
            get
            {
#if UNITY_EDITOR
                return @"Z:\appstore\diablo2\mpq2zip\";
#else
                return Application.persistentDataPath;
#endif
            }
        }

        public MainMenu mainMenuPrefab;
        private DataLoader.LoadProgress loadProgress;
        private bool _pathsInited = false;
        private DataLoader.Paths _paths;
        private DataLoader.Paths paths
        {
            get
            {
                if (!_pathsInited)
                {
                    _paths = new DataLoader.Paths
                    {
                        mpq = new[]
                        {
                            new DataLoader.MpqLocation{filename= Path.Combine(MpqDir, "d2exp.zip"), optional=false},
                            new DataLoader.MpqLocation{filename= Path.Combine(MpqDir, "d2data.zip"), optional=false},
                            new DataLoader.MpqLocation{filename= Path.Combine(MpqDir, "d2char.zip"), optional=false},
                            new DataLoader.MpqLocation{filename= Path.Combine(MpqDir, "d2sfx.zip"), optional=true},
                            new DataLoader.MpqLocation{filename= Path.Combine(MpqDir, "d2music.zip"), optional=true},
                            new DataLoader.MpqLocation{filename= Path.Combine(MpqDir, "d2xMusic.zip"), optional=true},
                            new DataLoader.MpqLocation{filename= Path.Combine(MpqDir, "d2xtalk.zip"), optional=true},
                            new DataLoader.MpqLocation{filename= Path.Combine(MpqDir, "d2speech.zip"), optional=true},
                        },
                        animData = @"data\global\animdata.d2",
                    };
                }
                return _paths;
            }
        } 
        
        void Awake()
        {
            Debug.LogFormat("Application.persistentPath: {0}", Application.persistentDataPath);
            if (Application.isEditor)
            {
                DataPath = Application.streamingAssetsPath;
            }
            else
            {
                DataPath = Application.persistentDataPath;
            }
            Materials.Initialize();
            AudioManager.Initialize();
            Datasheet.SetLocation(typeof(BodyLoc), "data/global/excel/bodylocs.txt");
            Datasheet.SetLocation(typeof(SoundInfo), "data/global/excel/Sounds.txt");
            var dataLoader = new DataLoader(paths);
            loadProgress = dataLoader.LoadAll();
            ScreenMessage.Show("Loading... ");
        }

        void Update()
        {
            if (loadProgress.finished)
            {
                if (loadProgress.exception != null)
                {
                    string message = BuildExceptionMessage(loadProgress.exception);
                    ScreenMessage.Show(message);
                }
                else
                {
                    ScreenMessage.Hide();
                    Instantiate(mainMenuPrefab);
                }
                Destroy(gameObject);
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    GameManager.QuitGame();
            }
        }

        private string BuildExceptionMessage(Exception exception)
        {
            if (exception is FileNotFoundException)
            {
                return BuildMessage(exception.Message);
            }
            else
            {
                return exception.Message;
            }
        }

        private string BuildMessage(string missingFile)
        {
            string message = "File not found: " + missingFile;
            message += "\n\nBlizzard Diablo II resources are required";
            message += "\n\nCopy MPQ files to the Diablerie folder";
            return message;
        }
    }
}