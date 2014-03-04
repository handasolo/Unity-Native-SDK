using UnityEngine;
using System.Collections;
using System;
using SimpleJSON;


class PendingRequest {
	public Ajax ajax;  // outstanding POST /play request
	public int retryCount; // number of times we've retried this request
}

class Active {
	public JSONNode play;  // POST /play response from server
	public Boolean started; // true if we started playback
	public Boolean canSkip; // true if we can skip this song
	public int retryCount;  // number of times we've unsuccessfully asked server if we can start play
}

public class DoSomething : MonoBehaviour {

	public string apiServerBase = "https://feed.fm/api/v2";
	public string token =  "ac6a67377d4f3b655b6aa9ad456d25a29706355e";
	public string secret = "13b558028fc5d244c8dc1a6cc51ba091afb0be02";
	public string formats = "ogg";
	public int maxBitrate = 128;
	public string placementId;
	public string stationId;
	

	private string clientId;
	private JSONNode placement;
	private JSONNode stations;
	private Active active;
	private PendingRequest pendingRequest;
	private JSONNode pendingPlay;


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
		pendingRequest = null;
		pendingPlay = null;

		// stop playback of current song and set status to waiting
		assignCurrentPlay(null, true);

		// pull information about the placement
		yield return StartCoroutine(GetPlacementInformation());

		// then start queueing up plays
		yield return StartCoroutine(RequestNextPlay());
	}

	private void assignCurrentPlay(JSONNode play, bool waitingIfEmpty) {
		// remove any existing play
		if (active != null) {
			// trigger play-completed
			active = null;
		}

		if (play == null) {
			// nothing to play now

			if (waitingIfEmpty) {
				// status = 'waiting'
				// nothing to play... waiting

			} else {
				// status = 'idle'
				// trigger plays-exhausted

			}

		} else {
			active = new Active {
				play = play,
				canSkip = false,
				started = false,
				retryCount = 0
			};

			// status = active

			// trigger play-active
		}
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
		if (pendingRequest) {
			// we're already waiting for a play to come in
			yield break;
		}

		while (true) {
			Ajax ajax = new Ajax(Ajax.RequestType.POST, apiServerBase + "/play");
			ajax.addParameter("formats", formats);
			ajax.addParameter("client_id", clientId);
			ajax.addParameter("max_bitrate", maxBitrate);

			if (!String.IsNullOrEmpty(placementId)) {
				ajax.addParameter("placement_id", placementId);
			}

			if (!String.IsNullOrEmpty(stationId)) {
				ajax.addParameter ("station_id", stationId);
			}

			// let the rest of the code know we're awaiting a response
			pendingRequest = new PendingRequest {
				ajax = ajax,
				retryCount = 0
			};

			yield return StartCoroutine(SignedRequest(ajax));

			if ((pendingRequest == null) || (pendingRequest.ajax != ajax)) {
				// another request snuck in while waiting for the response to this one,
				// so we don't care about this one any more - just quit

				yield break;
			}

			if (ajax.success) {
				if (active) {
					// play this when the current song is complete
					pendingPlay = ajax.response["play"];

				} else {
					// start playing this right now, since nothing else is active
					assignCurrentPlay (ajax.response["play"]);
				}

				yield break;

			} else if (ajax.error == (int) FeedError.NoMoreMusic) {
				if (active) {
					// ran out of music to play, but we're still playing something, so
					// just make a note here
					pendingPlay = null;

				} else {
					// ran out of music, and nothing else to play
					// trigger plays-exhausted

				}

				yield break;

			} else if (ajax.error == (int) FeedError.NotUS) {
				// user isn't in the united states, so can't play anything
				// trigger not-in-us

				yield break;

			} else {
				// some unknown error 
				pendingRequest.retryCount++;

				// wait for an increasingly long time before retrying
				yield return WaitForSeconds(0.5f * Math.Pow(2.0, pendingRequest.retryCount));

			}
		}
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
