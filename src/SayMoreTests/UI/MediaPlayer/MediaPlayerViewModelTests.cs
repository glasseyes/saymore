using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using SayMore.UI.MediaPlayer;
using SayMoreTests.UI.Utilities;

namespace SayMoreTests.UI.MediaPlayer
{
	/// ----------------------------------------------------------------------------------------
	[TestFixture]
	public class MediaPlayerViewModelTests
	{
		MediaPlayerViewModel _model;
		MemoryStream _stream;

		/// ------------------------------------------------------------------------------------
		[SetUp]
		public void Setup()
		{
			_model = new MediaPlayerViewModel();
			_stream = new MemoryStream();
			_model.SetStdInForTest(new StreamWriter(_stream));
		}

		/// ------------------------------------------------------------------------------------
		[TearDown]
		public void TearDown()
		{
			_stream.Close();
		}

		/// ------------------------------------------------------------------------------------
		[Test]
		public void MakeTimeString_WithHHMMSSms_VerifyCorrect()
		{
			var ts = new TimeSpan(0, 2, 32, 54, 430);
			var seconds = float.Parse(ts.TotalSeconds.ToString());
			Assert.AreEqual("02:32:54.4", MediaPlayerViewModel.MakeTimeString(seconds));
		}

		/// ------------------------------------------------------------------------------------
		[Test]
		public void MakeTimeString_WithMMSSms_VerifyCorrect()
		{
			var ts = new TimeSpan(0, 0, 16, 23, 560);
			var seconds = float.Parse(ts.TotalSeconds.ToString());
			Assert.AreEqual("16:23.6", MediaPlayerViewModel.MakeTimeString(seconds));
		}

		/// ------------------------------------------------------------------------------------
		[Test]
		public void MakeTimeString_WithSSms_VerifyCorrect()
		{
			var ts = new TimeSpan(0, 0, 0, 47, 368);
			var seconds = float.Parse(ts.TotalSeconds.ToString());
			Assert.AreEqual("47.4", MediaPlayerViewModel.MakeTimeString(seconds));
		}

		/// ------------------------------------------------------------------------------------
		[Test]
		public void HandlePlayerOutput_SendEOF_GetsPlaybackEndedEvent()
		{
			bool eventCalled = false;
			_model.PlaybackEnded += ((sender, e) => eventCalled = true );
			_model.HandlePlayerOutput("EOF code:");
			Assert.IsTrue(eventCalled);
		}

		/// ------------------------------------------------------------------------------------
		[Test]
		public void HandlePlayerOutput_SendEOF_GetsMediaQueuedEvent()
		{
			var file = LoadMediaFile();
			bool eventCalled = false;
			_model.MediaQueued += ((sender, e) => eventCalled = true);

			try
			{
				_model.HandlePlayerOutput("EOF code:");
				Assert.IsTrue(eventCalled);
			}
			finally
			{
				File.Delete(file);
			}
		}

		/// ------------------------------------------------------------------------------------
		[Test]
		public void HandlePlayerOutput_SendPause_GetsPausedEvent()
		{
			bool eventCalled = false;
			_model.PlaybackPaused += ((sender, e) => eventCalled = true);
			_model.HandlePlayerOutput("ID_PAUSED");
			Assert.IsTrue(eventCalled);
		}

		/// ------------------------------------------------------------------------------------
		[Test]
		public void HandlePlayerOutput_SendPositionChangeWhenPaused_GetsPlaybackResumedEvent()
		{
			_model.HandlePlayerOutput("ID_PAUSED");
			Assert.IsTrue(_model.IsPaused);

			bool eventCalled = false;
			_model.PlaybackResumed += ((sender, e) => eventCalled = true);
			_model.HandlePlayerOutput("A:  24.8");
			Assert.IsTrue(eventCalled);
		}

		/// ------------------------------------------------------------------------------------
		[Test]
		public void HandlePlayerOutput_SendPositionChange_GetsPositionChangedEvent()
		{
			bool eventCalled = false;
			_model.PlaybackPositionChanged += ((sender, e) => eventCalled = true);
			_model.HandlePlayerOutput("A:  24.8");
			Assert.IsTrue(eventCalled);
		}

		/// ------------------------------------------------------------------------------------
		[Test]
		public void ToggleVolumeMute_Call_TogglesProperty()
		{
			Assert.IsFalse(_model.IsVolumeMuted);
			_model.ToggleVolumeMute();
			Assert.IsTrue(_model.IsVolumeMuted);
			_model.ToggleVolumeMute();
			Assert.IsFalse(_model.IsVolumeMuted);
		}

		/// ------------------------------------------------------------------------------------
		[Test]
		public void SetVolume_SetTooLow_Ignores()
		{
			var vol = _model.Volume;
			bool eventCalled = false;
			_model.VolumeChanged += ((sender, e) => eventCalled = true);
			_model.SetVolume(-1f);
			Assert.IsFalse(eventCalled);
			Assert.AreEqual(vol, _model.Volume);
		}

		/// ------------------------------------------------------------------------------------
		[Test]
		public void SetVolume_SetTooHigh_Ignores()
		{
			var vol = _model.Volume;
			bool eventCalled = false;
			_model.VolumeChanged += ((sender, e) => eventCalled = true);
			_model.SetVolume(101f);
			Assert.IsFalse(eventCalled);
			Assert.AreEqual(vol, _model.Volume);
		}

		/// ------------------------------------------------------------------------------------
		[Test]
		public void SetVolume_ChangeValue_GetsEvent()
		{
			var vol = _model.Volume;
			bool eventCalled = false;
			_model.VolumeChanged += ((sender, e) => eventCalled = true);
			_model.SetVolume(vol + 10);
			Assert.IsTrue(eventCalled);
			Assert.AreEqual(vol + 10, _model.Volume);
		}

		/// ------------------------------------------------------------------------------------
		[Test]
		public void SetVolume_ChangeValue_ChangesValue()
		{
			var vol = _model.Volume;
			_model.SetVolume(vol + 10);
			Assert.AreEqual(vol + 10, _model.Volume);
		}

		/// ------------------------------------------------------------------------------------
		[Test]
		public void SetVolume_ChangeValue_WritesToInputStream()
		{
			var vol = _model.Volume + 10;
			_model.SetVolume(vol);

			var expected = string.Format("volume {0} 1 \r\n", vol);
			Assert.AreEqual(expected, Encoding.ASCII.GetString(_stream.ToArray()));
		}

		/// ------------------------------------------------------------------------------------
		[Test]
		public void Pause_Call_WritesToInputStream()
		{
			_model.Pause();
			VerifyStreamContent("pause \r\n");
		}

		/// ------------------------------------------------------------------------------------
		[Test]
		public void Stop_Call_WritesToInputStream()
		{
			_model.ConfigurePlayingForTest();
			_model.Stop();
			VerifyStreamContent("stop \r\n");
		}

		/// ------------------------------------------------------------------------------------
		[Test]
		public void Seek_Call_WritesToInputStream()
		{
			_model.Seek(32.6f);
			VerifyStreamContent("seek 32.6 2\r\n");
		}

		/// ------------------------------------------------------------------------------------
		[Test]
		public void Play_CallWhenPaused_WritesToInputStream()
		{
			_model.ConfigurePlayingForTest();
			_model.HandlePlayerOutput("ID_PAUSED");
			_model.Play();
			VerifyStreamContent("pause \r\n");
		}

		/// ------------------------------------------------------------------------------------
		[Test]
		public void Play_CallWhenNotPaused_DoesNotWriteToInputStream()
		{
			_model.ConfigurePlayingForTest();
			_model.Play();
			VerifyStreamContent(string.Empty);
		}

		/// ------------------------------------------------------------------------------------
		[Test]
		public void LoadFile_Called_CallsQueuesMediaEvent()
		{
			bool eventCalled = false;
			_model.MediaQueued += ((sender, e) => eventCalled = true);
			var file = LoadMediaFile();

			try
			{
				Assert.IsTrue(eventCalled);
			}
			finally
			{
				File.Delete(file);
			}
		}

		/// ------------------------------------------------------------------------------------
		[Test]
		public void LoadFile_Called_MediaInfoAcquired()
		{
			Assert.IsNull(_model.MediaInfo);
			var file = LoadMediaFile();

			try
			{
				// Don't really need to check any more MediaInfo
				// properties since other test fixtures will do that.
				Assert.IsNotNull(_model.MediaInfo);
				Assert.IsTrue(_model.MediaInfo.IsVideo);
			}
			finally
			{
				File.Delete(file);
			}
		}

		/// ------------------------------------------------------------------------------------
		[Test]
		public void LoadFile_Called_TerminatesPlayback()
		{
			_model.ConfigurePlayingForTest();
			Assert.IsTrue(_model.HasPlaybackStarted);
			var file = LoadMediaFile();

			try
			{
				Assert.IsFalse(_model.HasPlaybackStarted);
			}
			finally
			{
				File.Delete(file);
			}
		}

		/// ------------------------------------------------------------------------------------
		private string LoadMediaFile()
		{
			var file = MPlayerMediaInfoTests.GetTestVideoFile();

			try
			{
				_model.LoadFile(file);
			}
			catch
			{
				File.Delete(file);
				throw;
			}

			return file;
		}

		/// ------------------------------------------------------------------------------------
		private void VerifyStreamContent(string expected)
		{
			Assert.AreEqual(expected, Encoding.ASCII.GetString(_stream.ToArray()));
		}
	}
}
