using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;

/*
 * Wrapper for the crappy WWW and WWWForm classes. This class
 * takes care of formatting parameters properly and parsing out
 * the JSON responses that feed.fm returns.
 * 
 * This might make a good general purpose wrapper for WWW if
 * we didn't have the 'success' and jSON logic in it.
 * 
 */

namespace FeedFm {
	
	public class Ajax {

		/*
		 * Request parameters
		 */

		public enum RequestType { GET, POST };

		public string url
		{
			get;
			private set;
		}

		public RequestType type 
		{
			get;
			private set;
		}

		private Dictionary<string, string> fields = new Dictionary<string, string>();
		private Hashtable headers = new Hashtable();
		private WWW www;

		/*
		 * Response data
		 */

		public JSONNode response
		{
			get;
			private set;
		}

		public bool success
		{
			get;
			private set;
		}

		public int error
		{
			get;
			private set;
		}

		public string errorMessage
		{
			get;
			private set;
		}



		public Ajax(RequestType type, string url) {
			this.type = type;
			this.url = url;
		}

		public void addParameter(string name, string value) {
			fields.Add (name, value);
		}

		public void addHeader(string name, string value) {
			headers.Add (name, value);
		}

		public IEnumerator Request() {
			WWWForm form = new WWWForm();

			fields.Add ("force200", "1");			

			if (type == RequestType.POST) {
				if (fields.Count == 0) {
					form.AddField ("ju", "nk");

				} else {
					foreach (KeyValuePair<string, string> kp in fields) {
						form.AddField (kp.Key, kp.Value);
					}
				}

				www = new WWW(url, form.data, headers);
				
			} else {
				if (fields != null) {
					url = url + ToQueryString(fields);
				}
				
				www = new WWW(url, null, headers);
				
			}
			
			yield return www;
			
			if (String.IsNullOrEmpty(www.error)) {
				try {
					// all responses should be JSON
					response = JSONNode.Parse(www.text);

					if (response["success"].AsBool) {
						success = true;
						yield break;

					} else {
						success = false;
						error = response["error"]["code"].AsInt;
						errorMessage = response["error"]["message"];

						yield break;

					}
					
				} catch (Exception) {
					success = false;
					yield break;
				}

			} else {
				success = false;
				error = 500;
				errorMessage = www.text;
				yield break;

			}
		}

		private string ToQueryString(Dictionary<string, string> nvc) {
			string query = "";
			
			foreach (KeyValuePair<string, string> pair in nvc) {
				if (query.Length > 0) {
					query += "&";
				} else {
					query += "?";
				}
				
				query += string.Format("{0}={1}", WWW.EscapeURL(pair.Key), WWW.EscapeURL(pair.Value));
			}
			
			return query;
		}

		public void DebugResponse() {
			if (success) {
				Debug.Log ("Response: " + www.text);
			} else {
				Debug.Log ("Error id " + error + ", Response: " + www.text);
			}
		}

	}
}