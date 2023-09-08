using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

using LiveSplit.UI;

namespace LiveSplit.Model
{
    public class SavePausedRun
    {

        private static LiveSplitState CurrentState;
        private static bool eventAdded = false;

        public static void SavePausedRunState(LiveSplitState state)
        {
            CurrentState = state;
            if (!eventAdded)
            {
                CurrentState.OnPause += Save_OnPause;
                eventAdded = true;
            }
        }

        public static void Save_OnPause(object sender, EventArgs e)
        {
            IRun run = CurrentState.Run;
            string pathTo = run.FilePath.Substring(0, run.FilePath.LastIndexOf('\\'));
            string folder = removeInvalidChars(run.GameName);
            string fname = removeInvalidChars(run.CategoryName);

            // Get path (will be overwritten)
            string path = Path.Combine(pathTo, "in_progress", folder, fname, "saved_run.lsr");

            // Create path
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (FileStream fs = File.Create(path))
            {
                foreach (Segment s in run)
                {
                    WriteText(fs, s.SplitTime.RealTime + "\n");
                }
            }
        }

        public static void Load_Saved_Run(TimerModel model, string lsr)
        {
            IRun run = model.CurrentState.Run;
            string[] lines = File.ReadAllLines(lsr);
            for (int i = 0; i < lines.Length; i++)
            {
                Console.WriteLine(run[i].SplitTime);
                run[i].SplitTime = Time.ParseText(lines[i]);
                Console.WriteLine(run[i].SplitTime);
            }
            
        }

        private static void WriteText(FileStream fs, string value)
        {
            byte[] info = new UTF8Encoding(true).GetBytes(value);
            fs.Write(info, 0, info.Length);
        }

        private static string removeInvalidChars(string path)
        {
            return String.Concat(path.Split(Path.GetInvalidFileNameChars()));
        }
    }
}
