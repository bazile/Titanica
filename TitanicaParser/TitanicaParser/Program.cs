using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Extensions;
using AngleSharp.Parser.Html;
using TitanicaParser.Model;

namespace TitanicaParser
{
	class Program
	{
		static void Main(string[] args)
		{
			//MinifyAllHtml(GetHtmlPath());

			string zipPath = GetHtmlPath() + ".zip";
			Console.WriteLine(zipPath);
			List<TitanicPassenger> passengers = ParsePassengerListings(zipPath);
			ParsePassengerDetails(zipPath, passengers);

			//Console.WriteLine(passengers.Count(pax => pax.AgeMonths.HasValue));
			//Console.WriteLine(passengers.Count(pax => pax.BirthDate.HasValue));
			//Console.WriteLine(passengers.Count(pax => pax.DeathDate.HasValue));

			//bool isValid = true;
			//Console.WriteLine(passengers.Count == 2139);
			//Console.WriteLine(passengers.Count(pax => pax.Sex == Sex.Unknown));

			// Save to XML
			var serializer = new XmlSerializer(typeof(List<TitanicPassenger>));
			using (var fstream = File.Create("titanic.xml"))
			{
				serializer.Serialize(fstream, passengers);
			}
		}

		static string GetHtmlPath()
		{
			DirectoryInfo di = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

			do
			{
				string path = Path.Combine(di.FullName, "Html", "encyclopedia-titanica");
				if (Directory.Exists(path)) return path;

				di = di.Parent;
			} while (di != null);

			throw new InvalidOperationException("Каталог encyclopedia-titanica не найден");
		}

		static List<TitanicPassenger> ParsePassengerListings(string zipPath)
		{
			List<TitanicPassenger> passengers = new List<TitanicPassenger>();

			using (var fstream = File.OpenRead(zipPath))
			using (var zip = new ZipArchive(fstream, ZipArchiveMode.Read))
			{
				foreach (var zipEntry in zip.Entries.Where(e => e.IsRootFile()))
				{
					string html = zipEntry.ReadContentAsString();
					IHtmlDocument htmlDoc = new HtmlParser().Parse(html);

					foreach (var tr in htmlDoc.QuerySelector("#manifest").QuerySelectorAll("tr"))
					{
						var pax = new TitanicPassenger();
						var cells = tr.QuerySelectorAll("td").ToArray();
						if (cells.Length == 0) continue;

						pax.Url = cells[0].QuerySelector("a[itemprop=\"url\"]").GetAttribute("href");

						pax.HasSurvived = !pax.Url.StartsWith("/titanic-victim/");
						pax.FamilyName = cells[0].QuerySelector("*[itemprop=\"familyName\"]").TextContent;
						pax.GivenName = cells[0].QuerySelector("*[itemprop=\"givenName\"]").TextContent;
						pax.HonorificPrefix = cells[0].QuerySelector("*[itemprop=\"honorificPrefix\"]").TextContent;
						if (pax.HonorificPrefix.AnyOf("Doña", "Miss", "Mlle", "Mme.", "Ms", "Mrs", "Sra."))
							pax.Sex = Sex.Female;
						else if (pax.HonorificPrefix.AnyOf("Captain", "Col.", "Colonel", "Don.", "Dr", "Fr", "Major", "Master", "Mr", "Rev.", "Revd", "Sir", "Sr."))
							pax.Sex = Sex.Male;
						else
						{
							if (pax.FullName.EndsWith(", Lady") || pax.FullName.EndsWith(", Countess of")) pax.Sex = Sex.Female;
						}

						string ageText = cells[1].TextContent.Trim();
						pax.AgeMonths = ageText.Length == 0 ? null : ageText.EndsWith("m") ? (int?)int.Parse(ageText.TrimEnd('m')) : (int?)12 * int.Parse(ageText);

						string classText = cells[2].TextContent;
						if (classText.Contains("1st Class Passenger"))
							pax.Class = Class.First;
						else if (classText.Contains("2nd Class Passenger"))
							pax.Class = Class.Second;
						else if (classText.Contains("3rd Class Passenger"))
							pax.Class = Class.Third;
						else if (classText.Contains("Deck Crew"))
							pax.Class = Class.DeckCrew;
						else if (classText.Contains("Engineering Crew"))
							pax.Class = Class.EngineeringCrew;
						else if (classText.Contains("Victualling Crew"))
							pax.Class = Class.VictuallingCrew;
						
						pax.IsGuaranteeGroupMember = classText.Contains("H&W Guarantee Group");
						if (classText.Contains("Servant")) pax.IsServant = true;

						pax.TicketNo = cells[3].InnerHtml.Split("<br>")[0].Trim();
						pax.TicketPrice = new Price(cells[3].InnerHtml.Split("<br>")[1].Trim());

						pax.Boarded = (City)Enum.Parse(typeof(City), cells[4].TextContent);

						pax.JobTitle = cells[5].TextContent.Replace("&nbsp;", "").Trim();
						if (pax.JobTitle.Length == 0)
						{
							//if (classText.Contains("Servant")) pax.Job = "Servant";
						}

						string lifeboatText = cells[6].TextContent.Trim();
						pax.Lifeboat = lifeboatText.Length > 0 && !lifeboatText.Contains("[") ? lifeboatText : null;
						passengers.Add(pax);
					}
				}
			}

			return passengers;
		}

		static void ParsePassengerDetails(string zipPath, List<TitanicPassenger> passengers)
		{
			var allProperties = new HashSet<string>();
			var allStrongProperties = new HashSet<string>();

			using (var fstream = File.OpenRead(zipPath))
			using (var zip = new ZipArchive(fstream, ZipArchiveMode.Read))
			{
				Uri baseUri = new Uri("https://www.encyclopedia-titanica.org");
				foreach (var pax in passengers)
				{
					string zipEntryPath = new Uri(baseUri, pax.Url).PathAndQuery.TrimStart('/');
					var zipEntry = zip.GetEntry(zipEntryPath);
					if (zipEntry == null) continue;

					string html = zipEntry.ReadContentAsString();
					IHtmlDocument htmlDoc = new HtmlParser().Parse(html);
					IElement personElement = htmlDoc.QuerySelector(".sidebar > div[itemtype='http://schema.org/Person']");

					// Properties
					var properties = (personElement ?? htmlDoc.DocumentElement).QuerySelectorAll("span[itemprop]").Select(el => new {
						itemprop = el.GetAttribute("itemprop"),
						content = el.GetAttribute("content"),
						text = el.TextContent.Trim()
					}).Where(p => !string.IsNullOrEmpty(p.content) || !string.IsNullOrEmpty(p.text))
					.ToArray();
					allProperties.AddRange(properties.Select(p => p.itemprop));

					if (properties.Length > 0)
					{
						pax.HonorificSuffix = properties.SingleOrDefault(p => p.itemprop == "honorificSuffix")?.text;
						pax.BirthDate = properties.SingleOrDefault(p => p.itemprop == "birthDate")?.content.ParseTitanicaDate();
						pax.DeathDate = properties.SingleOrDefault(p => p.itemprop == "deathDate")?.content.ParseTitanicaDate();
						pax.BirthPlace = properties.SingleOrDefault(p => p.itemprop == "birthPlace")?.content;
						if (string.IsNullOrEmpty(pax.JobTitle))
						{
							pax.JobTitle = properties.SingleOrDefault(p => p.itemprop == "jobTitle")?.text;
						}

						AssertAreEqual("GivenName", pax.GivenName, properties.SingleOrDefault(p => p.itemprop == "givenName")?.text);
						AssertAreEqual("FamilyName", pax.FamilyName, properties.SingleOrDefault(p => p.itemprop == "familyName")?.text);
						AssertAreEqual("HonorificPrefix", pax.HonorificPrefix, properties.SingleOrDefault(p => p.itemprop == "honorificPrefix")?.text);
						AssertAreEqual("JobTitle", pax.JobTitle, properties.SingleOrDefault(p => p.itemprop == "jobTitle")?.text);
						//TODO: assert age
					}

					// Text after <strong> elements
					var moreProperties = (personElement ?? htmlDoc.DocumentElement).QuerySelectorAll("strong").Select(el => new {
						t1 = el.TextContent,
						t2 = string.Join(" ", el.ParentElement.ChildNodes.Skip(2).Select(n => n.TextContent.TrimStart(':', '.').Trim()))
					}).ToArray();
					allStrongProperties.AddRange(moreProperties.Select(p => p.t1));
					pax.CabinNo = moreProperties.SingleOrDefault(p => p.t1 == "Cabin No.")?.t2;
				}
			}
		}

		static async Task DownloadPassengerDetails(List<TitanicPassenger> passengers, string root)
		{
			return;

			var urls = passengers.Select(pax => pax.Url).OrderBy(url => url).ToList();

			Console.WindowWidth = 125;
			using (var fstream = new FileStream(Path.Combine(root, "encyclopedia-titanica.zip"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
			using (var zip = new ZipArchive(fstream, ZipArchiveMode.Update))
			{
				Uri baseUri = new Uri("https://www.encyclopedia-titanica.org");

				for (int i = 0; i < urls.Count; ++i)
				{
					Console.Write($"[{i + 1,4}/{urls.Count}] ");

					var uri = new Uri(baseUri, urls[i]);
					string entryPath = uri.PathAndQuery;
					if (zip.GetEntry(entryPath) != null)
					{
						Console.WriteLine($"Skipping {uri}");
						continue;
					}

					string html = await DownloadHtml(uri);

					if (html != null)
					{
						var entry = zip.GetEntry(entryPath) ?? zip.CreateEntry(entryPath);
						using (var entryStream = entry.Open())
						using (var writer = new StreamWriter(entryStream))
						{
							writer.Write(html);
						}
					}

					Thread.Sleep(5000);
				}
			}
		}

		static async Task<string> DownloadHtml(Uri address)
		{
			Console.Write($"Downloading {address} ... ");
			string html = null;
			using (var httpHandler = new HttpClientHandler())
			{
				httpHandler.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
				using (var http = new HttpClient(httpHandler))
				{
					http.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
					http.DefaultRequestHeaders.Add("Dnt", "1");
					http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1; rv:57.0) Gecko/20100101 Firefox/57.0");
					using (var response = await http.GetAsync(address))
					{
						if (response.IsSuccessStatusCode)
						{
							html = await response.Content.ReadAsStringAsync();
						}
						Console.WriteLine(response.IsSuccessStatusCode ? "done." : ((int)response.StatusCode).ToString());
					}
				}
			}
			return html;
		}

		#region Minification

		static void MinifyAllHtml(string root)
		{
			Console.WriteLine($"Minifying files in {root}");
			long before = 0, after = 0;
			foreach (string path in Directory.EnumerateFiles(root, "*.html", SearchOption.AllDirectories))
			{
				var info = new FileInfo(path);
				before += info.Length;

				MinifyHtml(path);
				Console.Write(".");

				info.Refresh();
				after += info.Length;
			}
			Console.WriteLine();
			Console.WriteLine("Before: {0:N0}", before);
			Console.WriteLine("After : {0:N0}", after);
			Console.WriteLine("%     : {0:F2}", 100.0 * after / before);
		}

		static void MinifyHtml(string filePath)
		{
			IHtmlDocument htmlDoc = new HtmlParser().Parse(File.ReadAllText(filePath));

			string[] selectors = {
				"table#myTable", // Contains photographs only
				"script", "noscript", "style",
				"link[rel='stylesheet']",
				"head > link[rel='apple-touch-icon']",
				"head > link[rel='shortcut icon']",
				"head > link[rel='manifest']",
				"head > link[rel='publisher']",
				"head > link[rel='alternate'][type='application/rss+xml']",
				"head > meta[property^='og:']",
				"head > meta[property^='fb:']",
				"head > meta[property^='dc:']",
				"head > meta[property='article:modified_time']",
				"head > meta[name='robots']",
				"head > meta[name='copyright']",
				"head > meta[name='viewport']",
				"head > meta[name='language']",
				"head > meta[name='google-translate-customization']",
				"head > meta[http-equiv='expires']",
				"head > meta[http-equiv='Content-Security-Policy']",
				"ins.adsbygoogle",
				"span[id^='ezoic-pub-ad-placeholder-']",
				"span.ezoic-ad",
				"form#form",
				"section#header-bg",
				"div#topbar",
				"div#breadcrumb", "section#breadcrumb",
				"div#refbox",
				"div#fb-root",
				"div.widget",
				"h3#comment",
				"div.mobile-hide",
				"div.comments",
				"div.push.push-5", "div.push.push-10",  "div.push.push-20", "div.push.push-30", "div.push.push-40",
				"div#crt-1.ezflad-47", "div#crt-4.ezflad-47",
				"a.back-to-top-button",
				"div.fb-like", "div.fb-quote",
				"div#dmo1",
				"header", "footer"
			};
			foreach (var selector in selectors)
				foreach (var el in htmlDoc.QuerySelectorAll(selector)) el.Remove();

			foreach (var el in htmlDoc.QuerySelectorAll("*[style]")) el.RemoveAttribute("style");
			foreach (var el in htmlDoc.QuerySelectorAll("tr#infinite_scroll")) el.RemoveAttribute("id");

			htmlDoc.QuerySelector("body").ClearAttr();

			foreach (IComment comment in htmlDoc.Descendents<IComment>())
			{
				comment.Remove();
			}

			string html = htmlDoc.DocumentElement.InnerHtml;

			html = html.Replace("&nbsp;", " ");
			html = Regex.Replace(html, @"^\s+$", "", RegexOptions.Multiline);
			html = Regex.Replace(html, @"^\s+<", "<", RegexOptions.Multiline);

			//html.Length.ToString("N0").Dump();
			//File.WriteAllText(@"C:\Users\IEUser\Desktop\TRAINING\L11-S01-LINQ\Titanic\bin\Debug\TitanicData\titanic01-new.html", html);
			File.WriteAllText(filePath, html);
		}

		#endregion

		#region Assertions

		static void AssertAreEqual(string title, string s1, string s2)
		{
			if (string.IsNullOrEmpty(s1) == string.IsNullOrEmpty(s2)) return;
			if (string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase)) return;

			throw new ModelValidationException($"{title} mismatch: '{s1}' ≠ '{s2}'");
		}

		#endregion
	}
}
