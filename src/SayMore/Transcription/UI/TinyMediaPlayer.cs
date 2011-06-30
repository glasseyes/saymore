using System;
using System.Drawing;
using System.Windows.Forms;
using SayMore.Transcription.Model;
using SayMore.UI.MediaPlayer;
using SilTools;

namespace SayMore.Transcription.UI
{
	public partial class TinyMediaPlayer : UserControl
	{
		protected bool _mediaFileNeedsLoading = true;
		protected MediaPlayerViewModel _model;
		protected bool _showButtons;

		/// ------------------------------------------------------------------------------------
		public TinyMediaPlayer()
		{
			InitializeComponent();
			ForeColor = SystemColors.WindowText;
			BackColor = SystemColors.Window;
			SetStyle(ControlStyles.Selectable, false);
			DoubleBuffered = true;
			Font = FontHelper.MakeFont(SystemFonts.IconTitleFont, 7f);
			_buttonStop.Location = _buttonPlay.Location;

			_buttonPlay.Click += delegate { Play(); };
			_buttonStop.Click += delegate { Stop(); };

			_model = new MediaPlayerViewModel();
			_model.SetVolume(100);
			_model.Loop = true;
			_model.PlaybackStarted = (() => Invoke((Action)UpdateButtons));
			_model.PlaybackEnded = (() => Invoke((Action)HandlePlaybackStopped));
			_model.PlaybackPositionChanged = (pos => Invoke((Action<Rectangle>)(Invalidate), WaveFormRectangle));

			ShowButtons = true;
		}

		/// ------------------------------------------------------------------------------------
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				if (_model != null)
					_model.ShutdownMPlayerProcess();

				_model = null;
				components.Dispose();
			}

			base.Dispose(disposing);
		}

		/// ------------------------------------------------------------------------------------
		public string MediaFileName { get; private set; }

		/// ------------------------------------------------------------------------------------
		public ITimeOrderSegment Segment { get; private set; }

		/// ------------------------------------------------------------------------------------
		public void LoadSegment(string mediaFileName, ITimeOrderSegment segment)
		{
			if (segment != Segment)
			{
				MediaFileName = mediaFileName;
				Segment = segment;
				_mediaFileNeedsLoading = true;
				Invalidate(WaveFormRectangle);
			}
		}

		/// ------------------------------------------------------------------------------------
		public void LoadSegment(IMediaSegment segment)
		{
			if (segment != Segment)
			{
				MediaFileName = segment.MediaFile;
				Segment = segment;
				_mediaFileNeedsLoading = true;
				Invalidate(WaveFormRectangle);
			}
		}

		/// ------------------------------------------------------------------------------------
		protected Rectangle WaveFormRectangle
		{
			get
			{
				var rc = ClientRectangle;
				rc.Width -= (_buttonPlay.Width + Padding.Right);
				return rc;
			}
		}

		/// ------------------------------------------------------------------------------------
		public bool ShowButtons
		{
			get { return _showButtons; }
			set
			{
				_showButtons = value;
				UpdateButtons();
			}
		}

		/// ------------------------------------------------------------------------------------
		public void Play()
		{
			if (_model.HasPlaybackStarted)
				Stop();

			if (_mediaFileNeedsLoading)
			{
				_model.LoadFile(MediaFileName, Segment.Start, Segment.GetLength());
				_mediaFileNeedsLoading = false;
			}

			_model.Play();
		}

		/// ------------------------------------------------------------------------------------
		public void Stop()
		{
			if (_model.HasPlaybackStarted)
			{
				_model.Stop();
				UpdateButtons();
			}
		}

		/// ------------------------------------------------------------------------------------
		protected void UpdateButtons()
		{
			_buttonPlay.Visible = (ShowButtons && _model.IsPlayButtonVisible);
			_buttonStop.Visible = (ShowButtons && !_model.IsPlayButtonVisible);
		}

		/// ------------------------------------------------------------------------------------
		private void HandlePlaybackStopped()
		{
			Invalidate(WaveFormRectangle);
			UpdateButtons();
		}

		/// ------------------------------------------------------------------------------------
		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);
			Invalidate();
		}

		/// ------------------------------------------------------------------------------------
		protected override void OnEnter(EventArgs e)
		{
			base.OnEnter(e);
			Parent.Focus();
		}

		/// ------------------------------------------------------------------------------------
		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);

			if (Segment == null)
				return;

			var rc = WaveFormRectangle;

			if (!_mediaFileNeedsLoading)
			{
				// Draw bar indicating playback progress.
				var rcBar = rc;
				var pixelsPerSec = rcBar.Width / Segment.GetLength();
				var dx = (int)Math.Round(pixelsPerSec * (_model.CurrentPosition - Segment.Start),
					MidpointRounding.AwayFromZero);

				if (dx > 0)
				{
					rcBar.Width = (dx - rcBar.X);
					rcBar.Height -= 6;
					rcBar.Y += 3;
					using (var br = new SolidBrush(ColorHelper.CalculateColor(Color.White, BackColor, 110)))
						e.Graphics.FillRectangle(br, rcBar);
				}

				// Uncomment this to update playback position while playing back.
				//DrawTimeInfo(e.Graphics, _model.GetTimeDisplay(), rc, ForeColor, Color.Transparent);

				//// Draw vertical line indicating where is the playback position.
				//var pixelsPerSec = rc.Width / Segment.MediaLength;
				//var dx = (int)Math.Round(pixelsPerSec * (_model.CurrentPosition - Segment.MediaStart),
				//    MidpointRounding.AwayFromZero);

				//if (dx > 0)
				//{
				//    using (var pen = new Pen(ForeColor))
				//        e.Graphics.DrawLine(pen, rc.Left + dx, rc.Top, rc.Left + dx, rc.Bottom);
				//}
			}

			DrawTimeInfo(e.Graphics, Segment.Start, Segment.GetLength(), rc, ForeColor, Color.Transparent);
		}

		/// ------------------------------------------------------------------------------------
		public string GetTimeInfoDisplayText(float startPosition, float length)
		{
			return _model.GetTimeDisplay(startPosition, (length == 0 ? 0 :
				(float)((decimal)startPosition + (decimal)length)));
		}

		/// ------------------------------------------------------------------------------------
		public string GetRangeTimeInfoDisplayText(float startPosition, float length)
		{
			return _model.GetRangeTimeDisplay(startPosition, (length == 0 ? 0 :
				(float)((decimal)startPosition + (decimal)length)));
		}

		/// ------------------------------------------------------------------------------------
		public void DrawTimeInfo(Graphics g, float startPosition, float length, Rectangle rc,
			Color foreColor, Color backColor)
		{
			DrawTimeInfo(g, GetRangeTimeInfoDisplayText(startPosition, length), rc, foreColor, backColor);
		}

		/// ------------------------------------------------------------------------------------
		protected virtual void DrawTimeInfo(Graphics g, string text, Rectangle rc, Color foreColor, Color backColor)
		{
			using (var br = new SolidBrush(backColor))
				g.FillRectangle(br, rc);

			const TextFormatFlags flags = TextFormatFlags.WordEllipsis |
				TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Left;

			TextRenderer.DrawText(g, text, Font, rc, foreColor, backColor, flags);
		}
	}
}
