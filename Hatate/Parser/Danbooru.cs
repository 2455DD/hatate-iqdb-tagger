using System.Net;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using System.Text;
namespace Hatate.Parser
{
	class Danbooru : Page, IParser
	{
		/// <summary>
		/// For Danbooru we'll use the API instead of parsing the HTML page.
		/// </summary>
		/// <param name="url"></param>
		/// <returns></returns>
		public new bool FromUrl(string url)
		{
			string postId = url.Substring(url.LastIndexOf('/') + 1);

			if (postId == null) {
				return false;
			}

			using (WebClient webClient = new WebClient()) {
				webClient.Headers.Add("User-Agent", this.UserAgent);
				webClient.Encoding = Encoding.UTF8;
				string json = null;

				for (int i = 0; i < 3; i++)
				{
					try
					{
						json = webClient.DownloadString("https://danbooru.donmai.us/posts/" + postId + ".json");
						break;
					}
					catch (WebException webException)
					{
						HttpWebResponse response = (HttpWebResponse)webException.Response;
						if (response == null)
						{
							this.unreachable = true;
                            if (i==2) { return false; }
                            else { continue; }
						}
						if (response.StatusCode == HttpStatusCode.NotFound)
						{
							this.unavailable = true;
							return false;
						}
						return false;
					}
					catch
					{
						return false;
					}
				}
				return this.ParseJson(json);
			}
		}

		/*
		============================================
		Protected
		============================================
		*/

		override protected bool Parse(Supremes.Nodes.Document doc)
		{
			// Parsing is done by FromUrl()
			return true;
		}

		/*
		============================================
		Private
		============================================
		*/

		private bool ParseJson(string json)
		{
            if (string.IsNullOrWhiteSpace(json)) {
                return false;
            }
			JObject parsed;
            try { 
				parsed = JObject.Parse(json); 
			}
			catch(Newtonsoft.Json.JsonReaderException exception){
				var shift_jis = Encoding.GetEncoding(932);
				var utf8 = Encoding.UTF8;
				byte[] shiftJisBytes = shift_jis.GetBytes(json);
				byte[] utf8Bytes = Encoding.Convert(shift_jis, utf8, shiftJisBytes);
				string json_utf8 = utf8.GetString(utf8Bytes);
				parsed = JObject.Parse(json_utf8);

            }

            JToken rating = parsed.GetValue("rating");
            JToken width = parsed.GetValue("image_width");
            JToken height = parsed.GetValue("image_height");
            JToken size = parsed.GetValue("file_size");
            JToken deleted = parsed.GetValue("is_deleted");
			JToken banned = parsed.GetValue("is_banned");
            JToken full = parsed.GetValue("file_url");

            JToken tagStringGeneral = parsed.GetValue("tag_string_general");
            JToken tagStringCharacter = parsed.GetValue("tag_string_character");
            JToken tagStringCopyright = parsed.GetValue("tag_string_copyright");
            JToken tagStringArtist = parsed.GetValue("tag_string_artist");
            JToken tagStringMeta = parsed.GetValue("tag_string_meta");

            if (size != null) {
                long.TryParse(size.ToString(), out this.size);
            }

            if (width != null && height != null) {
                int.TryParse(width.ToString(), out this.width);
                int.TryParse(height.ToString(), out this.height);
            }

            if (full != null) {
                this.full = full.ToString();
            }

            if (rating != null) {
                switch (rating.ToString()) {
                    case "g": this.rating = "General"; break;
                    case "s": this.rating = "Safe"; break;
                    case "q": this.rating = "Questionable"; break;
                    case "e": this.rating = "Explicit"; break;
                }
            }

            if ((deleted != null && deleted.ToObject<bool>())
			||  (banned != null && banned.ToObject<bool>())) {
                this.unavailable = true;
            }

            this.AddTagsFromString(tagStringGeneral);
            this.AddTagsFromString(tagStringCharacter, "character");
            this.AddTagsFromString(tagStringCopyright, "series");
            this.AddTagsFromString(tagStringArtist, "creator");
            this.AddTagsFromString(tagStringMeta, "meta");

			return true;
        }

        private void AddTagsFromString(JToken tagsToken, string nameSpace = null)
		{
			if (tagsToken == null) {
				return;
			}

			string tagsString = tagsToken.ToString();

			if (string.IsNullOrWhiteSpace(tagsString)) {
				return;
			}

			string[] tags = tagsString.Split(' ');

			foreach (string tag in tags) {
				this.AddTag(tag.Replace('_', ' '), nameSpace);
			}
		}
	}
}
