using UnityEngine;
using System.Collections;
using SimpleJSON;
using System;
using System.Collections.Generic;

public class NetworkRequest : MonoBehaviour {

	private delegate void SuccessResponse(WWW request, JSONNode response);
	private delegate void ErrorResponse(WWW request);

	private enum RequestType { GET, POST };

	// 'POST' call with arguments
	private void Ajax(RequestType requestType, string url, Dictionary<string, string> fields, SuccessResponse success, ErrorResponse error) {
		StartCoroutine(AjaxRoutine(requestType, url, fields, success, error));
	}

	// 'GET' call
	private void Ajax(string url, SuccessResponse success, ErrorResponse error) {
		StartCoroutine (AjaxRoutine(RequestType.GET, url, null, success, error));
	}

	private IEnumerator AjaxRoutine(RequestType requestType, string url, Dictionary<string, string> fields, SuccessResponse success, ErrorResponse error) {
		WWW www;

		if ((requestType == RequestType.POST) || (fields != null)) {
			// note that technically this doesn't support a GET with parameters. we'll cross that bridge
			// when we come to it.
			WWWForm form = new WWWForm();

			if (fields != null) {
				foreach (string key in fields.Keys) {
					form.AddField(key, fields[key]);
				}
			} else {
				form.AddField("junk", "placeholder");
			}

			www = new WWW(url, form);

		} else {
			www = new WWW(url);

		}

		yield return www;

		if (www.error != null) {
			error(www);
		
		} else {
			try {
				// all responses should be JSON
				JSONNode jsonResponse = JSONNode.Parse(www.text);

				success(www, jsonResponse);

			} catch (Exception) {
				error(www);
			}			
		}
	}

	// Use this for initialization
	void Start () {
		Ajax("https://feed.fm/api/v2/oauth/time", 
	        	((WWW r, JSONNode d) => Debug.Log("success! time is " + d["time"])), 
                ((WWW r) => Debug.Log("error!")));

		Ajax (RequestType.POST, "https://feed.fm/api/v2/client", null,
		      ((WWW r, JSONNode d) => Debug.Log ("got a client id: " + d["client_id"])),
		      ((WWW r) => Debug.Log("error getting client id :" + r.text)));
	}
	
}
