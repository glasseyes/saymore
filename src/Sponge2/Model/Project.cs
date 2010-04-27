using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Palaso.Code;
using SilUtils;

namespace Sponge2.Model
{
	/// <summary>
	/// A project corresponds to a single folder (with subfolders) on the disk.
	/// In that folder is a file which persists the settings, then a folder of
	/// people, and another of sessions.
	/// </summary>
	[XmlType("Project")]
	public class Project
	{
		//autofac uses this, so that callers only need to know the path, not all the dependencies
		public delegate Project FactoryForNewProjects(string parentDirectoryPath, string projectName);
		public delegate Project FromFileFactory();

		private const string SessionFolderName = "sessions";
		public const string ProjectSettingsFileExtension = "sprj";

		public Session.Factory SessionFactory { get; set; }
		public Func<Session, Session> SessionPropertyInjectionMethod { get; set; }

		public Project(Session.Factory sessionFactory, Func<Session, Session> sessionPropertyInjectionMethod)
		{
			SessionFactory = sessionFactory;
			SessionPropertyInjectionMethod = sessionPropertyInjectionMethod;
		}

		[Obsolete("For xmldeserialization only")]
		public Project()
		{
		}

		/// <summary>
		/// Used for creating brand new projects
		/// </summary>
		public Project(string parentDirectoryPath, string projectName)
		{
			var projectDirectory = Path.Combine(parentDirectoryPath, projectName);
			RequireThat.Directory(parentDirectoryPath).Exists();
			RequireThat.Directory(projectDirectory).DoesNotExist();
			Directory.CreateDirectory(projectDirectory);
			Initialize(Path.Combine(projectDirectory, projectName + "." + ProjectSettingsFileExtension));
			Save();
		}

		/// <summary>
		/// Existing project factory method
		/// </summary>
		public static Project FromSettingsFilePath(string settingsFilePath, Func<Project,Project> injectProjectProperiesMethod)
		{
			if(!File.Exists(settingsFilePath))
			{
				throw new FileNotFoundException("Could not find the file.", settingsFilePath);
			}
			Exception e;
			var project = XmlSerializationHelper.DeserializeFromFile<Project>(settingsFilePath, out e);
			if (e != null)
			{
				throw e;
			}
			if (project == null) //TODO: what does this  XmlSerializationHelper actually do for us?
						// can it be fixed to not return null with no exception,as it now does?
			{
				throw new ApplicationException("Could not load the project");
			}
			project = injectProjectProperiesMethod(project);
			project.Initialize(settingsFilePath);
			return project;
		}


		public void Initialize(string settingsFilePath)
		{
			SettingsFilePath = settingsFilePath;

			Sessions = new List<Session>();
			People = new List<Person>();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Initializes the sessions for the project.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public void InitializeSessions()
		{
			if (!Directory.Exists(SessionsFolder))
				Directory.CreateDirectory(SessionsFolder);

			var allSessions = (from folder in SessionNames
					 orderby folder
					 select Session.LoadFromFolder(folder, SessionPropertyInjectionMethod));

			Sessions = (from session in allSessions
						where session != null	// sessions we couldn't load are null
						select session).ToList();
		}


		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the list of sorted session folders (including their full path) in the project.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[XmlIgnore]
		public string[] SessionNames
		{
			get
			{
				return (from x in Directory.GetDirectories(SessionsFolder)
						orderby x
						select x).ToArray();
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the full path to the folder in which the project's session folders are stored.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[XmlIgnore]
		public string SessionsFolder
		{
			get { return Path.Combine(ProjectFolder, SessionFolderName); }
		}

		/// ------------------------------------------------------------------------------------
		[XmlIgnore]
		protected string ProjectFolder
		{
			get
			{
				return Path.GetDirectoryName(SettingsFilePath);
			}
		}

		/// ------------------------------------------------------------------------------------
		public void Save()
		{
			XmlSerializationHelper.SerializeToFile(SettingsFilePath, this);
		}


		/// <summary>
		/// This is, roughly, the "ethnologue code", taken either from 639-2 (2 letters),
		/// or, more often, 639-3 (3 letters)
		/// </summary>
		public string Iso639Code { get; set; }


		/// <summary>
		/// Note: while the folder name will match the settings file name when it
		/// is first created, it needn't remain that way.
		/// A user can copy the project folder, rename
		/// it "blah (old)", whatever, and this will still work.
		/// </summary>
		[XmlIgnore]
		public string FolderPath
		{
			get
			{
				return Path.GetDirectoryName(SettingsFilePath);
			}
		}

		[XmlIgnore]
		public string SettingsFilePath
		{
			get; set;
		}

		/// ------------------------------------------------------------------------------------
		[XmlIgnore]
		public List<Session> Sessions { get; private set; }

		/// ------------------------------------------------------------------------------------
		[XmlIgnore]
		public List<Person> People { get; private set; }
	}
}
