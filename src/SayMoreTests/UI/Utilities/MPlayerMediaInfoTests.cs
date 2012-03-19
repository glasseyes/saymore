using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using SayMore.Media.UI;

namespace SayMoreTests.UI.Utilities
{
	[TestFixture]
	public class MPlayerMediaInfoTests
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Creates video file in the temp. folder. It's up to the caller to delete the file
		/// when finished.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string GetTestVideoFile()
		{
			return GetMediaResourceFile("SayMoreTests.Resources.shortVideo.wmv", "wmv");
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Creates audio file in the temp. folder. It's up to the caller to delete the file
		/// when finished.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string GetShortTestAudioFile()
		{
			return GetMediaResourceFile(Resources.shortSound, "wav");
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Creates audio file in the temp. folder. It's up to the caller to delete the file
		/// when finished.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static string GetLongerTestAudioFile()
		{
			return GetMediaResourceFile(Resources.longerSound,"wav");
		}

		/// ------------------------------------------------------------------------------------
		private static string GetMediaResourceFile(string resource, string mediaFileExtension)
		{
			var assembly = Assembly.GetExecutingAssembly();
			return GetMediaResourceFile(assembly.GetManifestResourceStream(resource), mediaFileExtension);
		}

		/// ------------------------------------------------------------------------------------
		private static string GetMediaResourceFile(Stream stream, string mediaFileExtension)
		{
			var path = Path.GetTempFileName();
			var mediaFilePath = Path.ChangeExtension(path, mediaFileExtension);
			File.Move(path, mediaFilePath);

			var buffer = new byte[stream.Length];
			stream.Read(buffer, 0, buffer.Length);
			stream.Close();
			stream.Dispose();

			using (var outStream = File.OpenWrite(mediaFilePath))
			{
				outStream.Write(buffer, 0, buffer.Length);
				outStream.Close();
			}

			return mediaFilePath;
		}

		/// ------------------------------------------------------------------------------------
		[Test]
		[Category("SkipOnTeamCity")]
		public void MPlayerMediaInfo_CreateAudio_ContainsCorrectInfo()
		{
			var tmpfile = GetShortTestAudioFile();

			try
			{
				var minfo = new MPlayerMediaInfo(tmpfile);
				Assert.IsFalse(minfo.IsVideo);
				Assert.AreEqual(tmpfile, minfo.FileName);
				Assert.AreEqual(1.45f, minfo.Duration);
				Assert.IsNull(minfo.FullSizedThumbnail);
			}
			finally
			{
				File.Delete(tmpfile);
			}
		}

		/// ------------------------------------------------------------------------------------
		[Test]
		[Category("SkipOnTeamCity")]
		public void MPlayerMediaInfo_CreateVideo_ContainsCorrectInfo()
		{
			var tmpfile = GetTestVideoFile();

			try
			{
				var minfo = new MPlayerMediaInfo(tmpfile);
				Assert.IsTrue(minfo.IsVideo);
				Assert.AreEqual(tmpfile, minfo.FileName);
				Assert.AreEqual(5.49f, (float)Math.Round(minfo.StartTime, 2));
				Assert.AreEqual(9.5f, (float)Math.Round(minfo.Duration, 2));
				Assert.AreEqual(new Size(320, 240), minfo.PictureSize);
				Assert.IsNotNull(minfo.FullSizedThumbnail);
			}
			finally
			{
				File.Delete(tmpfile);
			}
		}
	}
}
