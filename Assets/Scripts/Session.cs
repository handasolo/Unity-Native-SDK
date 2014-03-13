using UnityEngine;
using System.Collections;
using System;
using SimpleJSON;

/*
 * 'Session' talks to the Feed.fm servers and maintains the current active song
 * and queued up songs.
 * 
 * On creation, the session instance must minimally be given an API 'token' and 'secret'
 * that tell it where to pull music from. 
 * 
 * Music is organized into 'stations' that are grouped under 'placements'. The 
 * 'token' provided to the server maps to a default placement, and a placement has
 * a default station in it. You can optionally override those values by assigning
 * 'stationId' and 'placementId' values to this object.
 * 
 * To begin pulling in music, the Tune() method should be called. This method will
 * asynchronously obtain credentials from the server, identify the placement and
 * station to pull music from, and then start pulling 'plays' from the server.
 * 
 * After starting Tune(), this object will trigger the following events:
 * 
 * onPlacementChanged - called after the placement id is updated (either because you
 *   set it with 'placementId = XX' or a default value was retrieved from the server.
 * onStationChanged - called after the station id is updated (either because you 
 *   set it with 'stationId = XX' or a default value was retrieved from the server.
 * onPlacement - called after the server responds with data about the placement we are tuned to
 * onStations - called after the server responds with the list of stations in the current placement
 * 
 * onNotInUS - currently the service only supports users in the US. If the user is
 *   on an IP that we cannot geo-map to the US, then this event is triggered to notify
 *   that no music can be played and future interaction with the service is pointless.
 * onClientRegistered - all clients keep a persistent id that allows Feed.fm to enforce
 *   DMCA music playback rules. You really don't need to know the details of this, but
 *   when this id is retrieved from the server, we also geo-map the user's IP address, and
 *   this event can be considered a 'onInUS' event.
 * 
 * After the above events are sent out, this instance asks the server to create a new 'play'.
 * A 'play' holds the details of a single music track and a pointer to an audio file.
 * The returned play is called the 'active play'. This class sends an 'onPlayActive' event when
 * the play is returned from the server.
 * 
 * Once there is an active play, you may use the 'ReportPlayStarted', 'ReportPlayElapsed', and
 * 'ReportPlayCompleted' calls to inform the server about the status of playback.
 * 
 * Additionally, you can 'RequestSkip' to ask the server if the user may skip the current song
 * or 'RequestInvalid' to tell the server we're unable to play the current song for some technical
 * reason. If the server disallows a skip request, an 'onSkipDenied' event will be triggered
 * and nothing else will change. If the server allows a skip or invalid request, this object will
 * act as if a 'ReportPlayCompleted' call was made.
 * 
 * Calling 'ReportPlayCompleted' causes this object to discard the current active play, send
 * out an 'onPlayCompleted' event, and then try to request a new play from the server. (well,
 * technically this object will try to queue up the next play while you're working with the
 * current play, but you don't really need to know that). Eventually you'll get another
 * 'onPlayActive' just as when you first called 'Tune()'.
 * 
 * Because there are DMCA playback rules that prevent us from playing too many instances of
 * a particular artist, album, or track, there may be a time when Feed.fm can't find any more 
 * music to return in the current station. In that case, you'll get back an 'onPlaysExhausted'
 * event instead of an 'onPlayActive'. If you change stations or placements you might be
 * able to find more music - so you can change the stationId or placementId and then call 'Tune()'
 * again to get things moving again.
 * 
 * This class uses the 'SimpleJSON' package to represent the JSON responses from the server
 * in memory.
 * 
 * Some misc properties you can inspect:
 *   - placement - the current placement we're tuned to (if any)
 *   - stations - list of stations in the current placement
 *   - station - the current station we're tuned to (if any)
 *   - exhausted - if we've run out of music from the current station, this will be set
 *      to true until we change to a diffrent station
 *   - startedPlayback - this is true only after we've received a play from the current
 *      station and called 'reportPlayStarted'.
 *   - MaybeCanSkip() - returns true if we think the user can skip the current song. Note
 *      that we don't really know for sure if we can skip a song until the server tells us.
 *      If this returns false, then the user definitely can't skip the current song.
 *   - ResetClientId() - when testing, you will often get an 'onPlaysExhausted' if you skip
 *      through a bunch of songs in short order. This call will reset your client id and
 *      effectively erase your play history, freeing you to play music again. *NOTE* it
 *      is a violation of our terms of service to use this on production apps to allow users
 *      to avoid playback rules.
 *   - inUS - true if the server has mapped us to a US IP address. This is pessimistic and
 *     is false until we successfully get a song from the server. This boolean is a good way
 *     to hide the player UI until it is confirmed that we are in the US and can play music.
 * 
 * Some things to keep in mind:
 *   - A user might change IP addresses in the middle of a session and go from being in the US
 *     to not being in the US - in which case could get an 'onNotInUS' at just about any time.
 *     It's not a common thing, but it could happen.
 * 
 *   - The JSONNode objects returned are straight from the Feed.fm server. Look at the REST API
 *     responses to see how things are structured:
 * 
 *          https://developer.feed.fm/documentation#REST
 * 
 *     for instance, the 'play' object is documented here:
 * 
 *          https://developer.feed.fm/documentation#post-play
 * 
 *     and the station and placement objects are from here:
 * 
 *          https://developer.feed.fm/documentation#get-placement
 *     
 * Internally this class requests different audio formats based on the Unity environment
 * we're in. It boils down to OGG for desktop/web and MP3 for mobile. You don't need to worry
 * about this.
 * 
 * In the future we will surface the ability to request specific bitrates. 
 * 
 */


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

	public event Handler         onClientRegistered;// we successfully registered a client id from the server
	public event Handler         onNotInUS;			// the server won't give us music because we're outside the US

	/** Configuration **/

	public string token =  "ac6a67377d4f3b655b6aa9ad456d25a29706355e";
	public string secret = "13b558028fc5d244c8dc1a6cc51ba091afb0be02";

	/** Internal state **/

	private string apiServerBase = "https://feed.fm/api/v2";
	private string formats = "ogg"; // default, but updated in constructor for diff environments
	private string _placementId;
	private string _stationId;
	private string clientId;
	private string maxBitrate = "128";

	public JSONNode placement {
		get;
		private set;
	}

	public JSONNode stations {
		get;
		private set;
	}
	
	public JSONNode station {
		get {
			if (stations == null) {
				return null;
			}

			var stationArray = stations.AsArray;
			for (int i = 0; i < stationArray.Count; i++) {
				if (_stationId == (string) stationArray[i]["id"]) {
					return stationArray[i];
				}
			}

			return null;
		}
	}

	private Current current;
	private PendingRequest pendingRequest;
	private JSONNode pendingPlay;

	public JSONNode activePlay {
		get {
			if (current != null) {
				return current.play;
			} else {
				return null;
			}
		}
	}

	public bool exhausted {  // true if we've run out of music
		get;
		private set;
	}  

	public bool startedPlayback { // true if we have started music playback since startup or the last 'Tune'
		get;
		private set;
	}

	public bool inUS {
		get;
		private set;
	}

	/************** public API ******************/

	public virtual void Awake() {
#if UNITY_IPHONE
		formats = "mp3";
#endif

#if UNITY_ANDROID
		formats = "mp3";
#endif

		// we haven't started playing any music yet
		startedPlayback = false;

		// pessimistically assume we're out of the US
		inUS = false;
	}

	/*
	 * Start pulling in music
	 */

	public virtual void Tune() {
		if (string.IsNullOrEmpty(token)) {
			throw new Exception("no <token> value specified!");
		}
		
		if (string.IsNullOrEmpty(secret)) {
			throw new Exception(" no <secret> value specified!");
		}
		
		// abort any pending requests or plays
		pendingRequest = null;
		pendingPlay = null;

		// pretend we've got music available
		exhausted = false;

		// no music has started yet
		startedPlayback = false;

		// stop playback of current song and set status to waiting
		AssignCurrentPlay(null, true);

		// do some async shizzle
		StartCoroutine (TuneCoroutine ());
	}	

	/*
	 * Tell the server we've started playback of the active song
	 */

	public virtual void ReportPlayStarted() {
		if (current == null) {
			throw new Exception ("There is no active song to report that we have started");
		}

		startedPlayback = true;

		StartCoroutine (StartPlay (current.play));
	}

	/*
	 * Tell the server how much of the song we've listened to
	 */

	public virtual void ReportPlayElapsed(int seconds) {
		if (current == null) {
			throw new Exception ("Attempt to report elapsed play time, but the pay hasn't started");
		}

		Ajax ajax = new Ajax (Ajax.RequestType.POST, apiServerBase + "/play/" + current.play ["id"] + "/elapse");
		ajax.addParameter ("seconds", seconds.ToString ());
			
		StartCoroutine (SignedRequest (ajax));
	}

	/*
	 * Tell the server we completed playback of the current song
	 */

	public virtual void ReportPlayCompleted() {
		if ((current == null) || !current.started) {
			throw new Exception ("Attempt to report a play as completed when there is no active started play");
		}

		StartCoroutine (CompletePlay ());
	}

	/*
	 * Ask the server if we can skip the current song. This will ultimately trigger an 'onPlayCompleted' or
	 * 'onSkipDenied' event.
	 */

	public virtual void RequestSkip() {
		if (current == null) {
			throw new Exception("No song is active");
		}

		if (!current.started) {
			throw new Exception("No song has been started");
		}

		if (!current.canSkip) {
			if (onSkipDenied != null) onSkipDenied(this);
			return;
		}

		StartCoroutine(SkipPlay(current.play));
	}

	public virtual void RequestInvalidate() {
		if (current == null) {
			throw new Exception("No song is active");
		}

		StartCoroutine (InvalidatePlay(current.play));
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

	private IEnumerator StartPlay(JSONNode play) {
		while (true) {
			Ajax ajax = new Ajax(Ajax.RequestType.POST, apiServerBase + "/play/" + play["id"] + "/start");

			yield return StartCoroutine(SignedRequest(ajax));

			if ((current == null) || (current.play != play)) {
				// nobody cares about this song any more
				yield break;
			}

			if (ajax.success) {
				Debug.Log ("success on start!");

				current.canSkip = ajax.response["can_skip"].AsBool;
				current.started = true;

				if (onPlayStarted != null) onPlayStarted(this, play);

				Debug.Log ("looking for next song");
				// start looking for the next song
				yield return StartCoroutine(RequestNextPlay());

				Debug.Log ("done looking");
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
	 * Tell the server we've completed the current play, and make any pending
	 * play active.
	 */
	
	private IEnumerator CompletePlay() {
		Ajax ajax = new Ajax(Ajax.RequestType.POST, apiServerBase + "/play/" + current.play["id"] + "/complete");

		yield return StartCoroutine(SignedRequest (ajax));

		// we really don't care what the response was, really

		if (pendingRequest == null) {
			Debug.Log ("play was completed, and nothing is pending, so moving forward");

			// start playing whatever we've got queued up
			JSONNode pp = pendingPlay;
			pendingPlay = null;

			AssignCurrentPlay(pp);

		} else {
			Debug.Log ("play was completed... but there is a pending request, so waiting for that");

			// waiting for a request to come in, so kill current song and announce that we're waiting
			AssignCurrentPlay (null, true);
		}
	}

	/*
	 * Ask the server to skip the current song.
	 */

	private IEnumerator SkipPlay(JSONNode play) {
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
			current.canSkip = false;  // prevent future tries until we get next song

			if (onSkipDenied != null) onSkipDenied(this);

			yield break;
		}
	}

	private IEnumerator InvalidatePlay(JSONNode play) {
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
					Debug.Log ("playing queued up song");

					// skip to play already queued up
					JSONNode pp = pendingPlay;
					pendingPlay = null;

					AssignCurrentPlay(pp);

				} else {
					Debug.Log ("invalidating current song");

					// invalidate current song
					AssignCurrentPlay(null, true);

					// If nothing is queued up, that might be because we haven't tried to 'start'
					// this play yet, triggering the 'requestNextPlay'. So trigger it here.
					yield return StartCoroutine(RequestNextPlay());
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

	private IEnumerator TuneCoroutine() {

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
				Debug.Log ("nothing pending, but we'll wait");
				// status = 'waiting'
				// nothing to play... waiting

			} else {
				Debug.Log ("nothing pending, and we're not waiting!");
				// status = 'idle'

				exhausted = true;

				if (onPlaysExhausted != null) onPlaysExhausted(this);

			}

		} else {
			Debug.Log ("moving to new active song");

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
		if (!String.IsNullOrEmpty(_placementId) && (placement != null) && ((string) placement["id"] == _placementId)) {
			// we already have this placement loaded up
			yield break;
		}

		while (true) {
			Ajax ajax;

			if (String.IsNullOrEmpty(_placementId)) {
				ajax = new Ajax(Ajax.RequestType.GET, apiServerBase + "/placement");

			} else {
				ajax = new Ajax(Ajax.RequestType.GET, apiServerBase + "/placement/" + _placementId);
			
			}

			yield return StartCoroutine (SignedRequest (ajax));

			if (ajax.success) {
				placement = ajax.response["placement"];
				stations = ajax.response["stations"];

				if (String.IsNullOrEmpty(_placementId)) {
					if (onPlacementChanged != null) onPlacementChanged(this, _placementId);
				}

				if (_placementId != placement["id"]) {
					_placementId = placement["id"];

					if (onPlacement != null) onPlacement(this, placement);
				}

				if (String.IsNullOrEmpty(_stationId) && (stations.Count > 0)) {
					_stationId = stations[0]["id"];

					if (onStationChanged != null) onStationChanged(this, _stationId);
				}

				if (onStations != null) onStations(this, stations);				

				yield break;

			} else if (ajax.error == (int) FeedError.MissingObject) {
				
				// can't find placement - no point in continuing
				if (String.IsNullOrEmpty(_placementId)) {
					
					// no default placement
					throw new Exception("No default placement for these credentials");

				} else {

					// no such placement
					throw new Exception("No such placement with id " + _placementId);
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
			Debug.Log ("waiting for play to come in..");
			// we're already waiting for a play to come in
			yield break;
		}

		yield return StartCoroutine (EnsureClientId ());

		while (clientId != null) {
			Ajax ajax = new Ajax(Ajax.RequestType.POST, apiServerBase + "/play");
			ajax.addParameter("formats", formats);
			ajax.addParameter("client_id", clientId);
			ajax.addParameter("max_bitrate", maxBitrate);

			if (!String.IsNullOrEmpty(_placementId)) {
				ajax.addParameter("placement_id", _placementId);
			}

			if (!String.IsNullOrEmpty(_stationId)) {
				ajax.addParameter ("station_id", _stationId);
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
				Debug.Log ("ignoring response since another request has started");
				yield break;
			}

			if (ajax.success) {
				inUS = true;

				pendingRequest = null;
				
				if (current != null) {
					Debug.Log ("saving response as pending play");

					// play this when the current song is complete
					pendingPlay = ajax.response["play"];

				} else {
					Debug.Log ("playing response right now!");

					// start playing this right now, since nothing else is active
					AssignCurrentPlay (ajax.response["play"]);
				}

				yield break;

			} else if (ajax.error == (int) FeedError.NoMoreMusic) {
			
				if (current != null) {
					Debug.Log ("no more music");

					// ran out of music to play, but we're still playing something, so
					// just make a note here
					pendingPlay = null;

				} else {
					Debug.Log ("no more music to queue up");
					// ran out of music, and nothing else to play

					exhausted = true;

					if (onPlaysExhausted != null) onPlaysExhausted(this);

				}

				pendingRequest = null;

				yield break;

			} else if (ajax.error == (int) FeedError.NotUS) {
				Debug.Log ("not in us, sorry");

				// user isn't in the united states, so can't play anything
				inUS = false;

				if (onNotInUS != null) onNotInUS(this);

				yield break;

			} else {
				Debug.Log ("unknown error " + ajax.errorMessage);

				// some unknown error 
				pendingRequest.retryCount++;

				// wait for an increasingly long time before retrying
				yield return new WaitForSeconds(0.5f * (float) Math.Pow(2.0, pendingRequest.retryCount));

			}
		}
	}

	/*
	 * True if we're actively pulling audio from the server
	 */

	public bool IsTuned() {
		return (current != null) || (pendingRequest != null);
	}

	/*
	 * True if we've got an active play and we've started playback
	 */

	public bool HasActivePlayStarted() {
		return (current != null) && (current.started);
	}

	/*
	 * Return the currently active play, or null
	 */

	public JSONNode GetActivePlay() {
		if (current != null) {
			return current.play;

		} else {
			return null;
		}
	}

	/*
	 * Reset the cached client id. *for testing only!*
	 */

	public void ResetClientId() {
		PlayerPrefs.DeleteKey ("feedfm.client_id");
	}

	/*
	 * Ensure we've got a clientId
	 */

	private IEnumerator EnsureClientId() {
		if (clientId != null) {
			yield break;
		}

		if (PlayerPrefs.HasKey ("feedfm.client_id")) {
			// have one already, so use it
			clientId = PlayerPrefs.GetString("feedfm.client_id");

			if (onClientRegistered != null) onClientRegistered(this);

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

					if (onClientRegistered != null) onClientRegistered(this);

					yield break;
				
				} else if (ajax.error == (int) FeedError.NotUS) {

					// user isn't in the united states, so can't play anything
					inUS = false;

					if (onNotInUS != null) onNotInUS(this);

					yield break;
				}

				// no success, so wait bit and then try again
				yield return new WaitForSeconds(1.0f);
			}
		}
	}

	public bool MaybeCanSkip() {
		return ((current != null) && (current.started) && (current.canSkip));
	}

	public string placementId {
		get {
			return _placementId;
		}
		
		set {	
			if (_placementId == value) {
				return;
			}

			// abort any pending requests or plays
			pendingRequest = null;
			pendingPlay = null;
			
			// stop playback of current song
			AssignCurrentPlay(null, true);

			// pretend we've got music available
			exhausted = false;
			
			// no music has started yet
			startedPlayback = false;

			_placementId = value;

			if (onPlacementChanged != null) onPlacementChanged(this, _placementId);
		}
	}

	public string stationId {
		get {
			return _stationId;
		}
		
		set {
			if (_stationId == value) {
				return;
			}

			// abort any pending requests or plays
			pendingRequest = null;
			pendingPlay = null;
			
			// stop playback of current song
			AssignCurrentPlay(null, true);

			// pretend we've got music available
			exhausted = false;
			
			// no music has started yet
			startedPlayback = false;

			_stationId = value;

			if (onStationChanged != null) onStationChanged(this, _stationId);
		}
	}

}
