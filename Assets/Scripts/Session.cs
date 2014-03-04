using UnityEngine;
using System.Collections;
using System;
using SimpleJSON;


class PendingRequest {
	public Ajax ajax;  // outstanding POST /play request
	public int retryCount; // number of times we've retried this request
}

class Current {
	public JSONNode play;  // POST /play response from server
	public Boolean started; // true if we started playback
	public Boolean canSkip; // true if we can skip this song
	public int retryCount;  // number of times we've unsuccessfully asked server if we can start play
}

public class Session : MonoBehaviour {

	/** Events **/

	public delegate void IdHandler(Session obj, string id);
	public delegate void JSONNodeHandler(Session obj, JSONNode data);
	public delegate void Handler (Session obj);	

	public event JSONNodeHandler onPlacement;		// placement data was retrieved from the server
	public event JSONNodeHandler onStations;		// station data was retrieved from the server
	public event IdHandler       onPlacementChanged;// the current placement changed
	public event IdHandler       onStationChanged;	// the current station changed

	public event JSONNodeHandler onPlayActive;		// a play has become active and is ready to be started
	public event JSONNodeHandler onPlayStarted;		// the active play has started playback
	public event Handler         onSkipDenied;      // request to skip current song denied
	public event JSONNodeHandler onPlayCompleted;	// the active play has completed playback
	public event Handler         onPlaysExhausted;	// the server has no more songs for us in the current station

	public event Handler         onNotInUS;			// the server won't give us music because we're outside the US

	/** Configuration **/

	public string apiServerBase = "https://feed.fm/api/v2";
	public string token =  "ac6a67377d4f3b655b6aa9ad456d25a29706355e";
	public string secret = "13b558028fc5d244c8dc1a6cc51ba091afb0be02";
	public string formats = "ogg";
	public string maxBitrate = "128";
	public string placementId;
	public string stationId;

	/** Internal state **/

	private string clientId;
	private JSONNode placement;
	private JSONNode stations;
	private Current current;
	private PendingRequest pendingRequest;
	private JSONNode pendingPlay;

	// Use this for initialization
	void Start () {	
		/*

		onPlacement += (x, d) => Debug.Log ("got a new placement");
		onStations += (obj, data) => Debug.Log ("got a new list of stations");
		onPlacementChanged += (obj, id) => Debug.Log ("placement changed!");
		onStationChanged += (obj, id) => Debug.Log ("station changed");
		onPlayActive += (obj, data) => {
			reportPlayStarted();
		};
*/
		tune ();
	}

	/************** public API ******************/

	/*
	 * Start pulling in music
	 */

	public void tune() {
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
		AssignCurrentPlay(null, true);

		// do some async shizzle
		StartCoroutine (tuneCoroutine ());
	}	

	/*
	 * Tell the server we've started playback of the active song
	 */

	public void reportPlayStarted() {
		if (current == null) {
			throw new Exception ("There is no active song to report that we have started");
		}
		
		StartCoroutine (startPlay (current.play));
	}

	/*
	 * Tell the server how much of the song we've listened to
	 */

	public void reportPlayElapsed(int seconds) {
		if (current == null) {
			throw new Exception ("Attempt to report elapsed play time, but the pay hasn't started");
		}

		StartCoroutine (elapsePlay (seconds));
	}

	/*
	 * Tell the server we completed playback of the current song
	 */

	public void reportPlayCompleted() {
		if ((current == null) || !current.started) {
			throw new Exception ("Attempt to report a play as completed when there is no active started play");
		}

		StartCoroutine (completePlay ());
	}

	/*
	 * Ask the server if we can skip the current song. This will ultimately trigger an 'onPlayCompleted' or
	 * 'onSkipDenied' event.
	 */

	public void requestSkip() {
		if (current != null) {
			throw new Exception("No song is active");
		}

		if (current.started) {
			throw new Exception("No song has been started");
		}

		if (!current.canSkip) {
			if (onSkipDenied != null) onSkipDenied(this);
			return;
		}

		StartCoroutine(skipPlay());
	}

	public void requestInvalidate() {
		if (current != null) {
			throw new Exception("No song is active");
		}

		StartCoroutine (invalidatePlay());
	}

		/************** internal API ******************/

	/*
	 * Send an ajax request to the server along with authentication information 
	 */
	
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

	/*
	 * Tell the server that we're starting playback of our active song
	 */

	private IEnumerator startPlay(JSONNode play) {
		while (true) {
			Ajax ajax = new Ajax(Ajax.RequestType.POST, apiServerBase + "/play/" + play["id"] + "/start");

			yield return StartCoroutine(SignedRequest(ajax));

			if ((current == null) || (current.play != play)) {
				// nobody cares about this song any more
				yield break;
			}

			if (ajax.success) {
				current.canSkip = ajax.response["can_skip"].AsBool;
				current.started = true;

				if (onPlayStarted != null) onPlayStarted(this, play);

				// start looking for the next song
				yield return StartCoroutine(RequestNextPlay());

				yield break;

			} else if (ajax.error == (int) FeedError.PlaybackStarted) {
				// we appear to have missed the response to the original start.
				// assume the song was good
				current.canSkip = true;
				current.started = true;

				if (onPlayStarted != null) onPlayStarted(this, play);

				// start looking for the next song
				yield return StartCoroutine(RequestNextPlay());

				yield break;
				
			} else {
				current.retryCount++;

				yield return new WaitForSeconds(2.0f);

				// try again later
			}

		}
	}

	/*
	 * Tell the server we've elapsed X seconds of play time
	 */
	
	private IEnumerator elapsePlay(int seconds) {
		Ajax ajax = new Ajax (Ajax.RequestType.POST, apiServerBase + "/play/" + current.play ["id"] + "/elapse");
		ajax.addParameter ("seconds", seconds.ToString ());
		
		yield return StartCoroutine (SignedRequest (ajax));
	}

	
	/*
	 * Tell the server we've completed the current play, and make any pending
	 * play active.
	 */
	
	private IEnumerator completePlay() {
		Ajax ajax = new Ajax(Ajax.RequestType.POST, apiServerBase + "/play/" + current.play["id"] + "/complete");

		yield return StartCoroutine(SignedRequest (ajax));

		// we really don't care what the response was, really

		if (pendingRequest == null) {
			// start playing whatever we've got queued up
			JSONNode pp = pendingPlay;
			pendingPlay = null;

			AssignCurrentPlay(pp);

		} else {
			// waiting for a request to come in, so kill current song and announce that we're waiting
			AssignCurrentPlay (null, true);
		}
	}

	/*
	 * Ask the server to skip the current song.
	 */

	private IEnumerator skipPlay(JSONNode play) {
		Ajax ajax = new Ajax(Ajax.RequestType.POST, apiServerBase + "/play/" + play["id"] + "/skip");

		yield return StartCoroutine(SignedRequest (ajax));

		if ((current == null) || (current.play != play)) {
			// current song has since changed - we don't care about this response any more
			yield break;
		}

		if (ajax.success) {
			if (pendingPlay != null) {
				// skip to play already queued up
				JSONNode pp = pendingPlay;
				pendingPlay = null;

				AssignCurrentPlay(pp);

			} else if (pendingRequest != null) {
				// we're waiting for a pending request to come in
				AssignCurrentPlay(null, true);

			} else {
				// we're probably out of music here, since a 'start' notification
				// should have already kicked off a 'requestNextPlay()' call
				AssignCurrentPlay(null);
			}

			yield break;

		} else {
			if (onSkipDenied != null) onSkipDenied(this);

			yield break;
		}
	}

	private IEnumerator invalidatePlay(JSONNode play) {
		int retryCount = 0;

		while (true) {
			Ajax ajax = new Ajax(Ajax.RequestType.POST, apiServerBase + "/play/" + play["id"] + "/invalidate");

			yield return StartCoroutine (SignedRequest (ajax));

			if ((current == null) || (current.play != play)) {
				// nboody cares about this song any more
				yield break;
			}

			if (ajax.success) {
				if (pendingPlay != null) {
					// skip to play already queued up
					JSONNode pp = pendingPlay;
					pendingPlay = null;

					AssignCurrentPlay(pp);

				} else {
					// invalidate current song
					AssignCurrentPlay(null, true);

					// If nothing is queued up, that might be because we haven't tried to 'start'
					// this play yet, triggering the 'requestNextPlay'. So trigger it here.
					if (pendingRequest != null) {
						yield return RequestNextPlay();
					}
				}

				yield break;

			} else {
				retryCount++;

				yield return new WaitForSeconds(0.2f * (float) Math.Pow(2.0, retryCount));

			}
		}
	}

	/*
	 * Get information about the placement we're tuning in to, and ask for a play
	 */

	private IEnumerator tuneCoroutine() {

		// pull information about the placement
		yield return StartCoroutine(GetPlacementInformation());

		// then start queueing up plays
		yield return StartCoroutine(RequestNextPlay());
	}

	/*
	 * Make the passed in play the active play. (note that the internal code calls
	 * this the 'current' play because 'active' overlaps with an existing Unity
	 * field.
	 */

	private void AssignCurrentPlay(JSONNode play, bool waitingIfEmpty = false) {
		// remove any existing play
		if (current != null) {

			if (onPlayCompleted != null) onPlayCompleted(this, current.play);

			current = null;
		}

		if (play == null) {
			// nothing to play now

			if (waitingIfEmpty) {
				// status = 'waiting'
				// nothing to play... waiting

			} else {
				// status = 'idle'
				if (onPlaysExhausted != null) onPlaysExhausted(this);

			}

		} else {
			current = new Current {
				play = play,
				canSkip = false,
				started = false,
				retryCount = 0
			};

			// status = active

			if (onPlayActive != null) onPlayActive(this, current.play);
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

					if (onPlacement != null) onPlacement(this, placement);
				}

				if (String.IsNullOrEmpty(stationId) && (stations.Count > 0)) {
					stationId = stations[0]["id"];

					if (onStationChanged != null) onStationChanged(this, stationId);
				}

				if (onStations != null) onStations(this, stations);

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
		if (pendingRequest != null) {
			// we're already waiting for a play to come in
			yield break;
		}

		yield return StartCoroutine (EnsureClientId ());

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
				if (current != null) {
					// play this when the current song is complete
					pendingPlay = ajax.response["play"];

				} else {
					// start playing this right now, since nothing else is active
					AssignCurrentPlay (ajax.response["play"]);
				}

				yield break;

			} else if (ajax.error == (int) FeedError.NoMoreMusic) {
				if (current != null) {
					// ran out of music to play, but we're still playing something, so
					// just make a note here
					pendingPlay = null;

				} else {
					// ran out of music, and nothing else to play
					if (onPlaysExhausted != null) onPlaysExhausted(this);

				}

				yield break;

			} else if (ajax.error == (int) FeedError.NotUS) {
				// user isn't in the united states, so can't play anything
				if (onNotInUS != null) onNotInUS(this);

				yield break;

			} else {
				// some unknown error 
				pendingRequest.retryCount++;

				// wait for an increasingly long time before retrying
				yield return new WaitForSeconds(0.5f * (float) Math.Pow(2.0, pendingRequest.retryCount));

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

					yield break;
				}

				// no success, so wait bit and then try again
				yield return new WaitForSeconds(1.0f);
			}
		}
	}

}
