using UnityEngine;
using System.Collections;
using System;
using SimpleJSON;


public class DoSomething : MonoBehaviour {

	public string apiServerBase = "https://feed.fm/api/v2";
	public string token =  "ac6a67377d4f3b655b6aa9ad456d25a29706355e";
	public string secret = "13b558028fc5d244c8dc1a6cc51ba091afb0be02";
	public string placementId;
	public string stationId;
	

	private string clientId;
	private JSONNode placement;
	private JSONNode stations;

	private IEnumerator SignedRequest(Ajax ajax) {
		// add in authentication header
		ajax.addHeader ("Authorization",
		                "Basic " + System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(token + ":" + secret)));

		Debug.Log ("querying " + ajax.url);

		yield return StartCoroutine(ajax.Request());

		if (!ajax.success && (ajax.error == (int) FeedError.BadCredentials)) {
			throw new Exception("Invalid credentials provided!");
		}

		ajax.DebugResponse();

		yield break;
	}

	// Use this for initialization
	IEnumerator Start () {
	
		yield return StartCoroutine(tune ());

	}

	public IEnumerator tune() {
		if (string.IsNullOrEmpty(token)) {
			throw new Exception("no <token> value specified!");
		}

		if (string.IsNullOrEmpty(secret)) {
			throw new Exception(" no <secret> value specified!");
		}

		// abort any pending requests or plays

		// stop playback of current song and set status to waiting

		// pull information about the placement
		yield return StartCoroutine(GetPlacementInformation());

		// then start queueing up plays
		yield return StartCoroutine(RequestNextPlay());
	}

	/*
	 * Request information about the placement referred to by placementId. If it is
	 * empty, then request a default placement and set placementId.	 
	 */

	private IEnumerator GetPlacementInformation() {
		if (!String.IsNullOrEmpty(placementId) && (placement != null) && (placement["id"] == placementId)) {
			// we already have this placement loaded up
			yield break;
		}

		while (true) {
			Ajax ajax;

			if (String.IsNullOrEmpty(placementId)) {
				ajax = new Ajax(Ajax.RequestType.GET, apiServerBase + "/placement");

			} else {
				ajax = new Ajax(Ajax.RequestType.GET, apiServerBase + "/placement/" + placementId);
			
			}

			yield return StartCoroutine (SignedRequest (ajax));

			if (ajax.success) {
				placement = ajax.response["placement"];
				stations = ajax.response["stations"];

				if (placementId != placement["id"]) {
					placementId = placement["id"];

					// trigger placement-changed
				}

				if (String.IsNullOrEmpty(stationId) && (stations.Count > 0)) {
					stationId = stations[0]["id"];

					// trigger station-changed
				}

				// trigger stations

				yield break;

			} else if (ajax.error == (int) FeedError.MissingObject) {
				
				// can't find placement - no point in continuing
				if (String.IsNullOrEmpty(placementId)) {
					
					// no default placement
					throw new Exception("No default placement for these credentials");

				} else {

					// no such placement
					throw new Exception("No such placement with id " + placementId);
				}
				
			}

			yield return new WaitForSeconds(0.5f);
		}
	}
	
	/*
	 * Ask the server to create a new play for us, and queue it up
	 */

	private IEnumerator RequestNextPlay() {
		// no thing at the moment
		Debug.Log ("requesting next play!");

		yield break;
	}

	/*
	 * Ensure we've got a clientId
	 */

	private IEnumerator EnsureClientId() {
		if (PlayerPrefs.HasKey ("feedfm.client_id")) {
			// have one already, so use it
			clientId = PlayerPrefs.GetString("feedfm.client_id");

			Debug.Log ("clientId is " + clientId);

			yield break;

		} else {
			// need to get an id

			while (true) {
				Ajax ajax = new Ajax(Ajax.RequestType.POST, apiServerBase + "/client");

				yield return StartCoroutine(SignedRequest (ajax));

				if (ajax.success) {
					clientId = ajax.response["client_id"];

					try {
						PlayerPrefs.SetString ("feedfm.client_id", clientId);

					} catch (PlayerPrefsException) {
						// ignore, *sigh*
					}

					Debug.Log ("clientId is " + clientId);

					yield break;
				}

				// no success, so wait bit and then try again
				yield return new WaitForSeconds(1.0f);
			}
		}
	}

}
