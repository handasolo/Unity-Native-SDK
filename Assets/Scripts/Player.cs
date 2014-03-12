using UnityEngine;
using System.Collections;
using System;
using SimpleJSON;

/*
 * The player starts in Idle, then toggles
 * back and forth between Playing and Paused,
 * then finishes with Exhausted when there is
 * no more music available to play.
 */

public enum PlayerState {
	Idle,
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

		audioSource = gameObject.AddComponent<AudioSource> ();
	}

	public override void Tune() {
		base.Tune ();
	}
	
	public void Play() {
		if (!IsTuned()) {
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

		audioSource.Stop ();

		// the song was skipped or invalidated, we got nothing else to work on
		if (activePlayState == null) {
			yield break;
		}

		activePlayState.soundCompleted = true;

		if (!activePlayState.playStarted) {
			// we never started the song.. let's report it as invalid so we advance to
			// the next song
			RequestInvalidate();

			yield break;
		}

		// wait for server to acknowledge our 'start' call before reporting the song complete
		float timeWaitedForCompletion = 0f;
		while (!activePlayState.startReportedToServer && (timeWaitedForCompletion < 2.0f)) {
			timeWaitedForCompletion += Time.deltaTime;
			yield return true;
		}

		ReportPlayCompleted ();
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
	 * Current state of the player
	 */

	public PlayerState currentState {
		get {
			if (exhausted) {
				return PlayerState.Exhausted;

			} else if (!startedPlayback) {
				return PlayerState.Idle;

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
