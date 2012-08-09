using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using Localization;
using Palaso.IO;
using Palaso.Reporting;
using SayMore.Media.MPlayer;


namespace SayMore.Transcription.Model
{
	/// <summary>
	/// Exports the text of a tier to an SRT format text file
	/// </summary>
	public class SRTFormatSubTitleExporter
	{
		public static void Export(string outputFilePath, TierBase tier)
		{
			const string ktimeFormat = "hh\\:mm\\:ss\\,ff";
			using (var stream = File.CreateText(outputFilePath))
			{
				int index = 1;
				foreach (var segment in tier.Segments)
				{
					stream.WriteLine(index);
					stream.WriteLine(segment.TimeRange.Start.ToString(ktimeFormat) + " --> " + segment.TimeRange.End.ToString(ktimeFormat));
					stream.WriteLine(segment.Text);
					stream.WriteLine();
					index++;
				}
			}
		}
	}
}