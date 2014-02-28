using UnityEngine;
using System.Collections;
using SimpleJSON;
using System;
using System.Collections.Generic;

public class NetworkRequest : MonoBehaviour {

	private delegate void WWWResponse(WWW request, JSONNode response);

	private enum RequestType { GET, POST };

	// 'POST' call with arguments
	private void Ajax(RequestType requestType, string url, Dictionary<string, string> fields, WWWResponse success, WWWResponse error) {
		StartCoroutine(AjaxRoutine(requestType, url, fields, success, error));
	}

	// 'GET' call
	private void Ajax(string url, 
	                  WWWResponse success, 
	                  WWWResponse error) {
		StartCoroutine (AjaxRoutine(RequestType.GET, url, null, success, error));
	}

	private IEnumerator AjaxRoutine(RequestType requestType, 
	                                string url, 
	                                Dictionary<string, string> fields, 
	                                WWWResponse success, 
	                                WWWResponse error) {
		WWW www;
		WWWForm form = new WWWForm();

		// add authentication headers
		var headers = form.headers;
		headers["Authorization"]="Basic " + System.Convert.ToBase64String(
			System.Text.Encoding.UTF8.GetBytes("ac6a67377d4f3b655b6aa9ad456d25a29706355e:13b558028fc5d244c8dc1a6cc51ba091afb0be02"));

		// Unity doesn't give us contents on non-200 response
		if (fields == null) {
			fields = new Dictionary<string, string>();
		}		
		fields["force200"] = "1";


		if (requestType == RequestType.POST) {
			if (fields != null) {
				foreach (KeyValuePair<string, string> pair in fields) {
					form.AddField(pair.Key, pair.Value);
				}

				// note that we're assuming there is at least one entry in
				// the dictionary, otherwise this call will fail
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
				JSONNode jsonResponse = JSONNode.Parse(www.text);

				if (jsonResponse["success"].AsBool) {
					success(www, jsonResponse);
				} else {
					Debug.Log ("success = false");
					error(www, jsonResponse);
				}

			} catch (Exception) {
				Debug.Log ("response is not parseable");
				error(www, null);
			}			

		} else if (www.error != null) {
			Debug.Log ("byte count is " + www.bytesDownloaded);
			Debug.Log ("bytes are " + System.Text.Encoding.Default.GetString (www.bytes));
			
			try {
				JSONNode jsonResponse = JSONNode.Parse (www.text);
				
				error(www, jsonResponse);

			} catch (Exception) {
				error(www, null);

			}

		} else {
			Debug.Log ("byte count is " + www.bytesDownloaded);
			Debug.Log ("bytes are " + System.Text.Encoding.Default.GetString (www.bytes));

			error(www, null);
		}
	}

	private string ToQueryString(Dictionary<string, string> nvc)
	{
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

	// Use this for initialization
	void Start () {
		/*
		Ajax("https://feed.fm/api/v2/oauth/time", 
        	 ((WWW r, JSONNode d) => Debug.Log("success! time is " + d["time"])), 
        	 ((WWW r) => Debug.Log("error!")));		
        	 */
		RequestClientId ("http://feed.localdomain:8000/missing");

	}

	private void RequestClientId(string url) {
		Ajax (RequestType.POST, 
		      //"https://feed.fm/api/v2/client", 
		      url,
		      null, 
		      OnClientSuccess,
		      OnClientError);
	}

	private void OnClientSuccess(WWW r, JSONNode d) {
		Debug.Log ("found a new client id " + d["client_id"]);
	}


	private void OnClientError(WWW r, JSONNode d) {
		WaitAndThen(2.0f, () => {
			RequestClientId ("http://feed.localdomain:8000/missing");
		});
	}	

	private void WaitAndThen(float time, Action action) {
		StartCoroutine (WaitAndThenCoRoutine(time, action));
	}

	private IEnumerator WaitAndThenCoRoutine(float time, Action action) {
		yield return new WaitForSeconds(time);

		action();
	}

}
