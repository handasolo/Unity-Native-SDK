using UnityEngine;
using System.Collections;
using System;
using SimpleJSON;

/*
 * This class extend the Session class to manage playing the audio passed to us
 * from the server. This class doesn't have any logic to talk to the Feed.fm service -
 * it only uses inherited methods for that.
 * 
 * Before you read about this class, you might want to peruse the docs at the top of the Session class.
 * 
 * To use this class, first create an instance and set the 'token' and 'secret' values
 * to what you were given on developer.feed.fm.
 * 
 * If you want to start queuing music up without immediately playing it, plus ensure that
 * the computer can contact the Feed.fm user, you may call the
 * 'Tune()' method, which will cause the instance to request the next song from the Feed.fm
 * service, but not begin playback. If you want music to start playing immediately, 
 * or you want to start playing music that was retrieved in an earlier Tune() call, call the 'Play()'
 * method.
 * 
 * During playback a song can be paused with 'Pause()' and them resumed with 'Play()'. As
 * song playback progresses, this class calls 'ReportPlayElapsed()' every so often to tell
 * the server how much of the song has been listened to.
 * 
 * When the current song finishes, or if a 'Skip()' call is made, another song will
 * be retrieved from the server and playback started.
 * 
 * This class adds two new events: 'onPlayPaused' and 'onPlayResumed' to notify when playback
 * has been paused or resumed.
 * 
 * This class  exposes a 'PlayerState' enum value in the 'currentState' property to simplify 
 * figuring out what to render. The player exists in one of the following states:
 * 
 *  - Idle: no music is being pulled from the server, or only a call to Tune() has been made. 
 *          This is the initial default state, and we return to this state if ever the station id
 *          or placement id is updated.
 * 
 *  - Tuning: A call to 'Play()' has been made, but we haven't received a response
 *            from the server or retrieved the audio file yet.
 * 
 *  - Playing / Paused: We have a song that we've begun playback of and we're either
 *                      currently playing the song or the song is paused. 
 * 
 *  - Exhausted: The server has run out of music in the current station that passes DMCA playback
 *               rules. The current placement id or station id can be changed, followed by a call
 *               to 'Tune()' or 'Play()' to request more music.
 * 
 */

public enum PlayerState {
	Idle,
	Tuning,
	Playing,
	Paused,
	Exhausted
}

class ActivePlayState {
	public string id;
	public bool startReportedToServer;
	public bool soundCompleted;
	public bool playStarted;
	public int previousPosition;
}

public class Player : Session {
	
	public event Handler onPlayPaused;
	public event Handler onPlayResumed;

	private bool paused = true;
	private ActivePlayState activePlayState;
	private AudioSource audioSource;
	private bool applicationPaused;
	private float elapseInterval = 10f;

	public void Start() {
		onPlayActive += OnPlayActive;
		onPlayStarted += OnPlayStarted;
		onPlayCompleted += OnPlayCompleted;
		onPlaysExhausted += OnPlaysExhausted;
		onPlacementChanged += OnPlacementChanged;
		onStationChanged += OnStationChanged;

		audioSource = gameObject.AddComponent<AudioSource> ();
	}

	public void Play() {
		if (!IsTuned()) {
			Debug.Log ("playing!");
			paused = false;

			Tune ();

		} else if ((GetActivePlay() != null) && (activePlayState != null) && paused) {
			// resume playback of song
			audioSource.Play();
			paused = false;

			if (onPlayResumed != null) onPlayResumed(this);
		}
	}
	
	public void Pause() {
		if (!HasActivePlayStarted() ||
		    (activePlayState == null) ||
		    paused) {
			Debug.Log ("can't pause, because not playing");
			return;
		}

		Debug.Log ("pausing!");

		audioSource.Pause ();
		paused = true;

		if (onPlayPaused != null) onPlayPaused(this);
	}
	
	public void Skip() {
		if (!HasActivePlayStarted()) {
			Debug.Log("can't skip a non-actively-playing song");

			// can't skip non-playing song
			return;
		}

		Debug.Log ("skipping!");

		RequestSkip();
	}
	
	private void OnPlayActive(Session s, JSONNode play) {
		Debug.Log("on plague now active");

		activePlayState = new ActivePlayState {
			id = play["id"],
			playStarted = false,
			startReportedToServer = false,
			soundCompleted = false,
			previousPosition = 0
		};

		StartCoroutine(PlaySound(play["id"], play["audio_file"]["url"], play["audio_file"]["duration_in_seconds"].AsFloat));
	}

	private IEnumerator PlaySound(string id, string url, float durationInSeconds) {
		Debug.Log ("creating www object");

		// start loading up the song
		var www = new WWW(url);

		Debug.Log ("getting audio clip");
		var clip = www.GetAudioClip (false, //false = 2D 
		                             true); // true = stream and play as soon as possible

		// wait for something we can play
		while (!clip.isReadyToPlay && (www.progress < 1)) {
			Debug.Log ("waiting for clip to be ready or error, progress is " + www.progress);

			// while waiting for clip, another one was queued up
			if ((activePlayState == null) || (activePlayState.id != id))
				yield break;

			yield return new WaitForSeconds(0.2f);
		}

		// make sure we're still controlling the active play
		if ((activePlayState == null) || (activePlayState.id != id)) {
			Debug.Log ("not controlling same play");
			yield break;
		}

		// this never seems to work
		if (!String.IsNullOrEmpty(www.error)) {
			Debug.Log ("error is not null");
			// something failed trying to get the stream - mark this as invalid
			RequestInvalidate();
			
			yield break;
		}

		Debug.Log ("progressing with playing the audio");

		audioSource.loop = false;
		audioSource.clip = clip;

		// wait for us to be unpaused
		while (paused) {
			yield return true;
		}

		audioSource.Play ();

		float lastElapse = 0f;

		while ((activePlayState != null) && (applicationPaused || audioSource.isPlaying || paused)) {
			float time = audioSource.time;

			// don't play past the duration of the song
			if (time >= durationInSeconds) {
				Debug.Log ("time has elapsed - quitting play wait loop");
				break;
			}

			// quit if another song became active in the meantime
			if (activePlayState.id != id) {
				audioSource.Stop ();
				yield break;
			}

			// tell the server we started the song
			if (!activePlayState.playStarted && (time > 0)) {
				ReportPlayStarted ();
				activePlayState.playStarted = true;
			}

			/* future optimization: start loading queued up song now that the current
			 *   one has completed loading
			if (www.progress == 1) {
			}
			*/

			// every X seconds, report elapsed time to the server
			if (time - lastElapse > elapseInterval) {
				ReportPlayElapsed((int) Math.Floor (time));

				lastElapse = time;
			}
			
			yield return true;
		}

		Debug.Log ("stopping " + id);

		// The song was skipped or invalidated
		if (activePlayState == null) {
			audioSource.Stop ();

			yield break;
		}

		// The song was skipped and another song took our place before
		// we got to here. Don't stop the audio because it's already
		// been replaced with the new audio clip.
		if (activePlayState.id != id) {
			yield break;
		}

		audioSource.Stop ();

		activePlayState.soundCompleted = true;

		if (!activePlayState.playStarted) {
			// we never started the song.. let's report it as invalid so we advance to
			// the next song
			RequestInvalidate();

			yield break;
		}

		// wait for server to acknowledge our 'start' call before reporting the song complete
		float timeWaitedForCompletion = 0f;
		while (activePlayState != null &&
		       activePlayState.id == id &&
			   !activePlayState.startReportedToServer && 
		       (timeWaitedForCompletion < 2.0f)) {
			timeWaitedForCompletion += Time.deltaTime;
			yield return true;
		}

		if ((activePlayState != null) && (activePlayState.id == id)) {
			ReportPlayCompleted ();
		}
	}

	/*
	 * Server has been told that we've started playback
	 */

	private void OnPlayStarted(Session s, JSONNode play) {
		if ((activePlayState != null) && (activePlayState.id == (string) play["id"])) {
			activePlayState.startReportedToServer = true;
		}
	}

	/*
	 * Server has been told that we completed playback of the current song
	 */

	private void OnPlayCompleted(Session s, JSONNode play) {
		Debug.Log ("onPlayCompleted, with ids " + play["id"] + " and activeplaystate is " + activePlayState.id);

		if ((activePlayState != null) && (activePlayState.id == (string) play["id"])) {
			Debug.Log ("completing song!");
			activePlayState = null;
			
			// force us into play mode in case we were paused and hit
			// skip to complete the current song
			paused = false;
		}
	}

	/*
	 * Take us out of pause if we've run out of songs
	 */

	private void OnPlaysExhausted(Session s) {
		paused = false;
	}

	/*
	 * Put us in pause mode if we change the station/placement, which
	 * causes us to go out of 'tune'
	 */

	private bool initialStation = true;
	private void OnStationChanged(Session s, string id) {
		if (!initialStation) paused = true;

		initialStation = false;
	}

	/*
	 * Put us in pause mode if we change the station/placement, which
	 * causes us to go out of 'tune'
	 */

	private bool initialPlacement = true;
	private void OnPlacementChanged(Session s, string id) {
		if (!initialPlacement) paused = true;

		initialPlacement = false;
	}

	/*
	 * Current state of the player
	 */

	public PlayerState currentState {
		get {
			if (exhausted) {
				return PlayerState.Exhausted;

			} else if (!startedPlayback && paused) {
				return PlayerState.Idle;

			} else if (!startedPlayback && !paused) {
				return PlayerState.Tuning;

			} else if (paused) {
				return PlayerState.Paused;
				
			} else {
				return PlayerState.Playing;
			
			}
		}
	}

	/* 
	 * Keep track of when the audio isn't playing paused, but just in the
	 * background
	 */

	void OnApplicationPause(bool pauseState) {		
		applicationPaused = pauseState;
	}

}
