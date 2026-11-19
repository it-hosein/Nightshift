using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Nightshift.EditorTools
{
    internal static class AssignmentArchive
    {
        const string Name = @"\A[A-Za-z][A-Za-z0-9-]*\z";
        const string Number = @"\A[0-9]+\z";

        public static string Filename(string first, string last, string number)
        {
            first = first.Trim(); last = last.Trim();
            number = Regex.Replace(number.Trim(), "[۰-۹٠-٩]",
                digit => ((int)char.GetNumericValue(digit.Value[0])).ToString(CultureInfo.InvariantCulture));
            if (!Regex.IsMatch(first, Name) || !Regex.IsMatch(last, Name) || !Regex.IsMatch(number, Number))
                throw new ArgumentException("Enter your first and last names with Latin letters. Enter the student ID as digits.");
            return $"GD_Hw2_{first}_{last}_{number}.zip";
        }

        public static string Repository(string url)
        {
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps || uri.Host != "github.com" || !uri.IsDefaultPort ||
                uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 ||
                !Regex.IsMatch(uri.AbsolutePath, @"\A/[A-Za-z0-9-]+/[A-Za-z0-9_.-]+/?\z"))
                throw new ArgumentException("Enter a GitHub repository URL, such as https://github.com/username/nightshift-hw2.");
            string path = uri.AbsolutePath.TrimEnd('/');
            if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) path = path.Substring(0, path.Length - 4);
            if (path.EndsWith("/", StringComparison.Ordinal)) throw new ArgumentException("Enter a GitHub repository name.");
            return "https://github.com" + path;
        }

        public static void Write(string destination, string buildFolder, string executable, string report, string repository)
        {
            string player = Path.Combine(buildFolder, executable);
            if (!File.Exists(player) || !Directory.Exists(Path.Combine(buildFolder, Path.GetFileNameWithoutExtension(executable) + "_Data")))
                throw new FileNotFoundException("Unity did not produce the player and its data folder.", player);
            if (!File.Exists(report)) throw new FileNotFoundException("The coursework report PDF is missing.", report);
            if (File.Exists(destination)) throw new IOException("A ZIP with this name already exists in the selected folder.");

            string partial = destination + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                using (var zip = ZipFile.Open(partial, ZipArchiveMode.Create))
                {
                    using (var writer = new StreamWriter(zip.CreateEntry("Repository.txt").Open()))
                        writer.WriteLine(Repository(repository));
                    zip.CreateEntryFromFile(report, "Report.pdf", CompressionLevel.Optimal);
                    foreach (var file in Directory.EnumerateFiles(buildFolder, "*", SearchOption.AllDirectories))
                    {
                        string relative = file.Substring(buildFolder.Length + 1).Replace(Path.DirectorySeparatorChar, '/');
                        if (relative.IndexOf("ButDontShipItWithYourGame", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        zip.CreateEntryFromFile(file, "Build/" + relative, CompressionLevel.Optimal);
                    }
                }
                using (var zip = ZipFile.OpenRead(partial))
                {
                    foreach (var entry in zip.Entries)
                    {
                        using (var input = entry.Open()) input.CopyTo(Stream.Null);
                    }
                }
                File.Move(partial, destination);
            }
            finally
            {
                if (File.Exists(partial)) File.Delete(partial);
            }
        }
    }
}
