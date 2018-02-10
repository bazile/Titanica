using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TitanicaParser
{
	static class Erweiterungsmethode
	{
		public static bool IsDirectory(this ZipArchiveEntry entry)
		{
			return entry.Length == 0;
		}

		public static bool IsRootFile(this ZipArchiveEntry entry)
		{
			return entry.Length>0 && !entry.FullName.Contains("/");
		}

		public static string ReadContentAsString(this ZipArchiveEntry entry)
		{
			using (var zipStream = entry.Open())
			using (var reader = new StreamReader(zipStream))
			{
				return reader.ReadToEnd();
			}
		}

		public static bool AnyOf(this string s, params string[] values)
		{
			foreach (var value in values)
			{
				if (s == value) return true;
			}
			return false;
		}

		public static string[] Split(this string s, string splitBy)
		{
			return s.Split(new[] { splitBy }, StringSplitOptions.None);
		}

		public static DateTime? ParseTitanicaDate(this string s)
		{
			if (s == null) return null;

			DateTime date;
			if (DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InstalledUICulture, DateTimeStyles.RoundtripKind, out date))
			{
				return date;
			}

			return null;
		}

		public static void AddRange<T>(this HashSet<T> hashSet, IEnumerable<T> collection)
		{
			foreach (var item in collection)
			{
				hashSet.Add(item);
			}
		}
	}
}
