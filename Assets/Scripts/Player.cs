using UnityEngine;
using System.Collections;
using SimpleJSON;

public enum PlayerState {
	Playing,
	Paused,
	Idle
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
	public event Handler onPlayLiked;
	public event Handler onPlayUnliked;
	public event Handler onPlayDisliked;

	private bool paused = true;
	private ActivePlayState activePlayState = null;
	private AudioSource audioSource;
	private bool applicationPaused;

	public Player() : base() {
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
			paused = false;

			if (onPlayResumed != null) onPlayResumed(this);
		}
	}
	
	public void Pause() {
		if (!HasActivePlayStarted() ||
		    (activePlayState == null) ||
		    paused) {
			return;
		}

		paused = true;

		if (onPlayPaused != null) onPlayPaused(this);
	}
	
	public void Skip() {
		if (!HasActivePlayStarted()) {
			// can't skip non-playing song
			return;
		}

		RequestSkip();
	}
	
	private void OnPlayActive(Session s, JSONNode play) {
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
		// start loading up the song
		var www = new WWW(url);
		
		var clip = www.GetAudioClip (false, /* false = 2D */
		                             true); /* true = stream and play as soon as possible */

		// wait for something we can play
		while (!clip.isReadyToPlay) {
			// while waiting for clip, another one was queued up
			if ((activePlayState == null) || (activePlayState.id != id))
				yield break;

			// TODO:
			// there should probably be something in here to deal with the case where we
			// never load up something successfully

			yield return new WaitForSeconds(0.2f);
		}

		// make sure we're still controlling the active play
		if ((activePlayState == null) || (activePlayState.id != id))
			yield break;
		
		audioSource.loop = false;
		audioSource.clip = clip;

		// wait for us to be unpaused
		while (paused) {
			yield return true;
		}

		audioSource.Play ();

		while ((activePlayState != null) && (applicationPaused || audioSource.isPlaying || paused)) {
			// don't play past the duration of the song
			if (audioSource.time >= durationInSeconds) {
				Debug.Log ("time has elapsed - quitting play wait loop");
				break;
			}

			// quit if another song became active in the meantime
			if (activePlayState.id != id) {
				audioSource.Stop ();
				yield break;
			}

			// tell the server we started the song
			if (!activePlayState.playStarted && (audioSource.time > 0)) {
				ReportPlayStarted ();
				activePlayState.playStarted = true;
			}

			// every X seconds, report elapsed time to the server
			
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
		// TODO: set a limit on this - maybe 2 seconds
		while (!activePlayState.startReportedToServer) {
			yield return true;
		}

		ReportPlayCompleted ();
	}

	/*
	 * Server has been told that we've started playback
	 */

	private void OnPlayStarted(Session s, JSONNode play) {
		if ((activePlayState != null) && (activePlayState.id == play["id"])) {
			activePlayState.startReportedToServer = true;
		}
	}

	/*
	 * Server has been told that we completed playback of the current song
	 */

	private void OnPlayCompleted(Session s, JSONNode play) {
		if ((activePlayState != null) && (activePlayState.id == play["id"])) {
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
	 * Keep track of when the audio isn't playing paused, but just in the
	 * background
	 */
	
	void OnApplicationPause(bool pauseState) {
		applicationPaused = pauseState;
	}

}
