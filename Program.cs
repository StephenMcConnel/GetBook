using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Xml;

namespace GetBook
{
	class MainClass
	{
		private const string RootUrl = "https://api.digitallibrary.io/book-api/opds/v1/root.xml";

		private static HttpClient _client = new HttpClient();
		private static XmlDocument _root = new XmlDocument();
		private static XmlNamespaceManager _nsmgr;

		public static void Main(string[] args)
		{
			GetRootPage();
			var entriesEnglish = GetAllEntriesForLanguage("en", "English");
			DownloadBooksByAuthor("Rohini Nilekani", entriesEnglish);
			var entriesFrench = GetAllEntriesForLanguage("fr", "French");
		}

		public static XmlDocument GetAllEntriesForLanguage(string code, string name)
		{
			var allEntries = GetRecordsForLanguage(code);
			allEntries.Save("/tmp/All"+name+"Entries.opds");
			return allEntries;
		}

		public static void GetRootPage()
		{
			var data = GetOpdsPage(RootUrl);
			_root.LoadXml(data);
			_nsmgr = new XmlNamespaceManager(_root.NameTable);
			_nsmgr.AddNamespace("lrmi", "http://purl.org/dcx/lrmi-terms/");
			_nsmgr.AddNamespace("opds", "http://opds-spec.org/2010/catalog");
			_nsmgr.AddNamespace("dc", "http://purl.org/dc/terms/");
			_nsmgr.AddNamespace("a", "http://www.w3.org/2005/Atom");
		}

		public static string GetOpdsPage(string urlPath)
		{
			Console.WriteLine("Retrieving OPDS page at " + urlPath);
			return _client.GetStringAsync(urlPath).Result;
		}

		public static XmlDocument GetRecordsForLanguage(string lang)
		{
			var allEntries = new XmlDocument();
			allEntries.LoadXml("<digitallibrary lang='en' xmlns:lrmi='http://purl.org/dcx/lrmi-terms/' xmlns:dc='http://purl.org/dc/terms/' xmlns='http://www.w3.org/2005/Atom'></digitallibrary>");
			var urlBase = GetBaseReferenceForLanguage(lang);
			if (String.IsNullOrEmpty(urlBase))
				return allEntries;
			var data = GetOpdsPage(urlBase);
			var basePage = new XmlDocument();
			basePage.LoadXml(data);
			var nextUrl = GetRelativeLink("first", basePage);
			while (nextUrl != null)
			{
				var nextData = GetOpdsPage(nextUrl);
				var currentPage = new XmlDocument();
				currentPage.LoadXml(nextData);
				// extract the entry nodes and duplicate/save them
				var entries = currentPage.SelectNodes("/a:feed/a:entry", _nsmgr);
				foreach (XmlNode node in entries)
				{
					var newEntry = allEntries.CreateElement("entry");
					var innerXml = node.InnerXml;
					innerXml = innerXml.Replace(" xmlns=\"http://www.w3.org/2005/Atom\"", "");
					newEntry.InnerXml = innerXml;
					allEntries.DocumentElement.AppendChild(newEntry);
				}
				nextUrl = GetRelativeLink("next", currentPage);
			}
			return allEntries;
		}

		public static string GetBaseReferenceForLanguage(string lang)
		{
			var node = _root.DocumentElement.SelectSingleNode("/a:feed/a:link[contains(@href, '/"+lang+"/root.xml')]", _nsmgr) as XmlElement;
			if (node != null)
				return node.GetAttribute("href");
			return null;
		}

		public static string GetRelativeLink(string relValue, XmlDocument currentPage)
		{
			var node = currentPage.DocumentElement.SelectSingleNode("/a:feed/a:link[contains(@rel, '"+relValue+"')]", _nsmgr) as XmlElement;
			if (node != null)
				return node.GetAttribute("href");
			return null;
		}

		public static void DownloadBooksByAuthor(string author, XmlDocument allEntries)
		{
			var entries = allEntries.DocumentElement.SelectNodes("//entry", _nsmgr);
			foreach (XmlNode entry in entries)
			{
				var name = entry.SelectSingleNode("./author/name", _nsmgr);
				if (name != null && name.InnerText == author)
				{
					var link = entry.SelectSingleNode("./link[@type='application/epub+zip']") as XmlElement;
					if (link != null)
					{
						var href = link.GetAttribute("href");
						var title = entry.SelectSingleNode("./title");
						if (!String.IsNullOrEmpty(href) && title != null && !String.IsNullOrEmpty(title.InnerText))
						{
							DownloadBook(href, title.InnerText);
						}
					}
				}
			}
		}

		public static void DownloadBook(string urlBook, string title)
		{
			var path = "/tmp/"+title+".epub";
			Console.WriteLine("Downloading and saving book to " + path);
			var bytes = _client.GetByteArrayAsync(urlBook).Result;
			File.WriteAllBytes("/tmp/"+title+".epub", bytes);
		}
	}
}
