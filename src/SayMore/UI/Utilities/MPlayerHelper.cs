using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Palaso.IO;
using SayMore.Model.Files;

namespace SayMore.UI.Utilities
{
	#region MPlayerHelper class
	/// ----------------------------------------------------------------------------------------
	public static class MPlayerHelper
	{
		private static readonly HashSet<int> s_mplayerProcessIds = new HashSet<int>();

		/// ------------------------------------------------------------------------------------
		public static string MPlayerPath
		{
			get { return FileLocator.GetFileDistributedWithApplication("mplayer", "mplayer.exe"); }
		}

		/// ------------------------------------------------------------------------------------
		private static MPlayerProcess GetNewMPlayerProcess()
		{
			CleanUpMPlayerProcesses();
			return new MPlayerProcess();
		}

		/// ------------------------------------------------------------------------------------
		private static bool StartProcess(Process prs)
		{
			if (prs.Start())
			{
				prs.PriorityClass = ProcessPriorityClass.High;
				s_mplayerProcessIds.Add(prs.Id);
				return true;
			}

			return false;
		}

		/// ------------------------------------------------------------------------------------
		public static void CleanUpMPlayerProcesses()
		{
			foreach (int id in s_mplayerProcessIds)
			{
				try
				{
					var prs = Process.GetProcessById(id);
					prs.Kill();
					prs.Close();
				}
				catch { }
			}

			s_mplayerProcessIds.Clear();
		}

		/// ------------------------------------------------------------------------------------
		public static MPlayerProcess StartProcessToMonitor(float startPosition, float volume, Int32 hwndVideo,
			DataReceivedEventHandler outputDataHandler, DataReceivedEventHandler errorDataHandler)
		{
			if (hwndVideo == 0)
				throw new ArgumentException("No window handle specified.");

			if (outputDataHandler == null)
				throw new ArgumentNullException("outputDataHandler");

			if (errorDataHandler == null)
				throw new ArgumentNullException("errorDataHandler");

			var prs = GetNewMPlayerProcess();
			prs.StartInfo.RedirectStandardInput = true;
			prs.StartInfo.RedirectStandardError = true;
			prs.OutputDataReceived += outputDataHandler;
			prs.ErrorDataReceived += errorDataHandler;
			prs.StartInfo.Arguments = GetMPlayerCommandLineForPlayback(startPosition, volume, hwndVideo);

			if (!StartProcess(prs))
			{
				// REVIEW: Revise to do something a little less drastic.
				prs = null;
				throw new ApplicationException("Unable to start mplayer.");
			}

			prs.StandardInput.AutoFlush = true;
			prs.BeginOutputReadLine();
			prs.BeginErrorReadLine();

			return prs;
		}

		/// ------------------------------------------------------------------------------------
		private static string GetMPlayerCommandLineForPlayback(float startPosition,
			float volume, int hwndVideo)
		{
			// If we find an MPlayer config file in the same path as this assembly, then
			// use that for the settings instead of our default set.
			var mplayerConfigPath = Path.Combine(GetPathToThisAssembly(), "MPlayerSettings.conf");

			string cmdLine;

			if (File.Exists(mplayerConfigPath))
			{
				cmdLine = string.Format("-include {0} -ss {1} -volume {2} -wid {3} ",
					mplayerConfigPath, startPosition, volume, hwndVideo);
			}
			else
			{
				cmdLine = string.Format("-slave -noquiet -idle " +
					"-msglevel identify=9:global=9 -nofontconfig -autosync 100 -priority abovenormal " +
					" −osdlevel 0 -ss {0} -volume {1} -fixed-vo -wid {2} ", startPosition, volume, hwndVideo);
#if !MONO
				cmdLine += " -vo gl";
#endif
			}

			return cmdLine;
		}

		/// ------------------------------------------------------------------------------------
		private static string GetPathToThisAssembly()
		{
			var assembly = Assembly.GetExecutingAssembly();
			var mplayerConfigPath = assembly.CodeBase.Replace("file:", string.Empty).TrimStart('/');
			return Path.GetDirectoryName(mplayerConfigPath);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Extracts the audio stream from the specified video file and writes it to a wav
		/// file (i.e. raw PCM).
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static void ExtractAudioToWave(string videoPath, string audioOutPath)
		{
			videoPath = videoPath.Replace('\\', '/');

			var prs = GetNewMPlayerProcess();
			prs.StartInfo.Arguments =
				string.Format("\"{0}\" -nofontconfig -vo null -vc null -ao pcm:fast:file=%{1}%\"{2}\"",
				videoPath, audioOutPath.Length, audioOutPath);

			StartProcess(prs);
		}

		///// ------------------------------------------------------------------------------------
		//public static MPlayerMediaInfo GetMediaInfo(string videoPath)
		//{
			//var prs = GetNewMPlayerProcess();
			//prs.OutputDataReceived += minfo.HandleOutputDataReceived;

			//videoPath = videoPath.Replace('\\', '/');
			//prs.StartInfo.Arguments = string.Format("-nocache -msglevel identify=6 " +
			//    "-nofontconfig -frames 0 -nosound -vc null -vo null \"{0}\"", videoPath);

			//if (StartProcess(prs))
			//{
			//    prs.BeginOutputReadLine();
			//    prs.WaitForExit();
			//    prs.Close();
			//    int seconds = Math.Min(8, (int)(minfo.MediaLength / 2));
			//    minfo.FullSizedThumbnail = GetImageFromVideo(videoPath, seconds);
			//}
			//else
			//{
			//    prs = null;
			//    Palaso.Reporting.ErrorReport.NotifyUserOfProblem(
			//        "Getting media length failed for file '{0}'.", videoPath);
			//}

			//return minfo;
		//}

		/// ------------------------------------------------------------------------------------
		public static Image GetImageFromVideo(string videoPath, int seconds)
		{
			Image img = null;
			videoPath = videoPath.Replace('\\', '/');
			var prs = GetNewMPlayerProcess();
			var tmpFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
			Directory.CreateDirectory(tmpFolder);
			var tmpFile = Path.Combine(tmpFolder, "00000001.jpg");

			try
			{
				prs.StartInfo.Arguments =
					string.Format("-nocache -nofontconfig -really-quiet -frames 1 -ss {0} -nosound -vo jpeg:outdir=\"\"\"{1}\"\"\" quality=100 \"{2}\"",
					seconds, tmpFolder, videoPath);

				StartProcess(prs);
				prs.WaitForExit();
				prs.Close();
				ComponentFile.WaitForFileRelease(videoPath);

				if (File.Exists(tmpFile))
				{
					// I could use Image.FromFile, but that leaves
					// a lock on the file, for some reason.
					var stream = new FileStream(tmpFile, FileMode.Open);
					img = Image.FromStream(stream);
					stream.Close();
				}
			}
			finally
			{
				if (File.Exists(tmpFile))
					File.Delete(tmpFile);

				try { Directory.Delete(tmpFolder); }
				catch { }
			}

			return img;
		}
	}

	#endregion

	#region MPlayerProcess class
	/// ----------------------------------------------------------------------------------------
	public class MPlayerProcess : Process
	{
		public string MediaFileName { get; set; }

		/// ------------------------------------------------------------------------------------
		public MPlayerProcess()
		{
			StartInfo.CreateNoWindow = true;
			StartInfo.UseShellExecute = false;
			StartInfo.RedirectStandardOutput = true;
			StartInfo.FileName = MPlayerHelper.MPlayerPath;
		}

		/// ------------------------------------------------------------------------------------
		public void KillAndWaitForFileRelease()
		{
			if (!HasExited)
				Kill();

			if (MediaFileName != null)
				ComponentFile.WaitForFileRelease(MediaFileName);
		}
	}

	#endregion

	#region MPlayerMediaInfo class
	/// ----------------------------------------------------------------------------------------
	public class MPlayerMediaInfo
	{
		public string FileName { get; private set; }
		public bool IsVideo { get; private set; }
		public float Duration { get; private set; }
		public float StartTime { get; private set; }
		public Size PictureSize { get; private set; }
		public Image FullSizedThumbnail { get; private set; }

		/// ------------------------------------------------------------------------------------
		public MPlayerMediaInfo(string filename)
		{
			FileName = filename;

			// Palaso uses FFMpeg, which seems to give a more accurate media length.
			var ffmpeginfo = Palaso.Media.MediaInfo.GetInfo(filename);
			ComponentFile.WaitForFileRelease(filename);

			IsVideo = (ffmpeginfo.Video != null);
			Duration = (float)ffmpeginfo.Audio.Duration.TotalSeconds;

			if (!IsVideo)
				return;

			// I don't understand it, but videos have a start time and a duration. Sometimes
			// the start time is zero, but for other videos it's not. The most accurate
			// duration when playing back in the player seems to be the sum of the duration
			// and the start time. Therefore, check if this media file has a start time to
			// add to the duration.
			var match = Regex.Match(ffmpeginfo.RawData, "Duration: .+, start: ");
			if (match.Success)
			{
				match = Regex.Match(ffmpeginfo.RawData.Substring(match.Index + match.Value.Length), ".+,");
				if (match.Success)
					StartTime = float.Parse(match.Value.TrimEnd(','));
			}

			try
			{
				var dimensions = ffmpeginfo.Video.Resolution.Split('x');
				PictureSize = new Size(int.Parse(dimensions[0]), int.Parse(dimensions[1]));
			}
			catch { }

			int seconds = Math.Min(8, (int)(Duration / 2));
			FullSizedThumbnail = MPlayerHelper.GetImageFromVideo(filename, seconds);
		}

		// This method can be restored if we ever use MPlayer to get the media information.
		///// ------------------------------------------------------------------------------------
		//public void HandleOutputDataReceived(object sender, DataReceivedEventArgs e)
		//{
		//    if (e.Data == null)
		//        return;

		//    if (e.Data.StartsWith("ID_LENGTH="))
		//        MediaLength += float.Parse(e.Data.Substring(10));
		//    else if (e.Data.StartsWith("ID_START_TIME=") && !e.Data.EndsWith("unknown"))
		//        MediaLength += float.Parse(e.Data.Substring(14));
		//    else if (e.Data.StartsWith("ID_VIDEO_WIDTH="))
		//        PictureSize = new Size(int.Parse(e.Data.Substring(15)), PictureSize.Height);
		//    else if (e.Data.StartsWith("ID_VIDEO_HEIGHT="))
		//        PictureSize = new Size(PictureSize.Width, int.Parse(e.Data.Substring(16)));
		//    else if (e.Data.StartsWith("ID_VIDEO_FORMAT"))
		//        IsVideo = true;
		//}
	}

	#endregion

	#region MPlayerOutputLogForm class
	/// ------------------------------------------------------------------------------------
	public class MPlayerOutputLogForm : Form
	{
		private readonly TextBox _textBox;

		/// --------------------------------------------------------------------------------
		public MPlayerOutputLogForm(string initialOutput)
		{
			_textBox = new TextBox();
			_textBox.ReadOnly = true;
			_textBox.BackColor = SystemColors.Window;
			_textBox.Multiline = true;
			_textBox.WordWrap = false;
			_textBox.ScrollBars = ScrollBars.Both;
			_textBox.Dock = DockStyle.Fill;
			_textBox.Font = SystemFonts.MessageBoxFont;

			var rc = Screen.PrimaryScreen.Bounds;
			Width = (rc.Width / 4);
			Height = (int)(rc.Height * 0.75);
			Location = new Point(rc.Right - Width, 0);
			StartPosition = FormStartPosition.Manual;
			Padding = new Padding(10);
			Text = @"Player Output Log";
			ShowIcon = false;
			ShowInTaskbar = false;
			MaximizeBox = false;
			MinimizeBox = false;
			Controls.Add(_textBox);

			UpdateLogDisplay(initialOutput);
		}

		/// --------------------------------------------------------------------------------
		public void UpdateLogDisplay(string output)
		{
			_textBox.SelectionStart = _textBox.Text.Length;
			_textBox.SelectedText = output + Environment.NewLine;
		}

		/// --------------------------------------------------------------------------------
		protected override bool ShowWithoutActivation
		{
			get { return true; }
		}
	}

	#endregion
}
