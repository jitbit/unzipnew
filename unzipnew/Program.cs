using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace unzipnew
{
	class Program
	{
		static void Main(string[] args)
		{
			if (args.Length == 0)
			{
				Console.WriteLine("No files specified");
				return;
			}

			string zipfile = args[0];
			if (!File.Exists(zipfile))
			{
				Console.WriteLine("Source does not exist");
				return;
			}

			string outputdir = Environment.CurrentDirectory;

			if (args.Length == 3 && args[1].ToLower() == "-d")
				outputdir = args[2];

			Directory.CreateDirectory(outputdir); //in case it does not exist

			using (var zipArchive = ZipFile.OpenRead(zipfile))
			{
				var entriesToExtract = new List<ZipArchiveEntry>();
				//enumerate entries and check md5, if differs - add to queue to extract later in one run (to make extraction faster and more atomic)
				foreach (var e in zipArchive.Entries)
				{
					string destPath = Path.Combine(outputdir, e.FullName);

					if(string.IsNullOrEmpty(e.Name) && !Directory.Exists(destPath)) //it's a directory - let's extract
						entriesToExtract.Add(e);

					if (!File.Exists(destPath)) //file not exists yet - lets extract
					{
						entriesToExtract.Add(e);
					}
					else //file exists, let's check its md5
					{
						byte[] zipHash; using (var zipStream = e.Open()) { zipHash = CalculateMd5(zipStream); }
						byte[] fileHash = CalculateMd5(destPath);

						if (!Enumerable.SequenceEqual(zipHash, fileHash)) //files differ, extract
							entriesToExtract.Add(e);
					}
				}

				if (!entriesToExtract.Any()) return;

				//now run through the entries and extract TO TEMP FOLDER first (faster)
				var tmpDir = GetTemporaryDirectory();
				foreach (var e in entriesToExtract)
				{
					string destPath = Path.Combine(tmpDir, e.FullName.Replace("/", "\\"));

					if (string.IsNullOrEmpty(e.Name)) //its a folder
						Directory.CreateDirectory(destPath);
					else
					{
						Console.WriteLine("Extacting: " + e.FullName);
						e.ExtractToFile(destPath, true);
					}
				}

				Console.WriteLine("Copying from temp dir to destination...");

				//copy files from tmp folder to destination (this is quicker than extracting to dest directly, we built this tool as a DEPLOY machinism so it need to work fast)
				DirectoryInfo diSource = new DirectoryInfo(tmpDir);
				DirectoryInfo diTarget = new DirectoryInfo(outputdir);
				CopyFilesRecursively(diSource, diTarget);

				//cleanup
				Directory.Delete(tmpDir, true);

				Console.WriteLine("Done.");
			}
		}

		private static byte[] CalculateMd5(Stream stream)
		{
			using (var md5 = MD5.Create())
			{
				return md5.ComputeHash(stream);
			}
		}

		private static byte[] CalculateMd5(string filePathName)
		{
			using (var stream = File.OpenRead(filePathName))
			{
				return CalculateMd5(stream);
			}
		}

		private static string GetTemporaryDirectory()
		{
			string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			Directory.CreateDirectory(tempDirectory);
			return tempDirectory;
		}

		public static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
		{
			foreach (DirectoryInfo dir in source.GetDirectories())
				CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
			foreach (FileInfo file in source.GetFiles())
				file.CopyTo(Path.Combine(target.FullName, file.Name), true);
		}

	}
}
