using UnityEngine;
using System.Collections;

public class SpeakerGUI : MonoBehaviour {

	/*
	 * 
	 * 
	 * or maybe I don't make this - I just make a Player object that observes an Audio Source.
	 * 
	 * that probably makes more sense, right?
	 * 
	 * audio = speaker.create() -
	 *   use the WWW class to create an AudioClip
	 * audio.play
	 *   AudioSource.Play
	 * audio.pause
	 *   AudioSource.Pause
	 * audio.resume
	 *   AudioSource.Play
	 * audio.destroy
	 *   AudioSource.Stop
	 * 
	 * 
	 * Assume the Player class extends the Session class.
	 * 
	 */

	private AudioSource audioSource;
	private bool paused = false;
	private bool applicationPaused;

	void OnApplicationPause(bool pauseState) {
		applicationPaused = pauseState;

		Debug.Log ("pause state is now: " + pauseState);
	}

	// Use this for initialization
	IEnumerator Start () {
		//Application.runInBackground = true;

		// this is basically the speaker
		Debug.Log ("creating audio source");
		audioSource = gameObject.AddComponent<AudioSource> ();
		
		yield return new WaitForSeconds (2.0f);

		Debug.Log ("playing sound");
		yield return StartCoroutine(Play ("https://d1jys12wsigh0s.cloudfront.net/files/2/63/XfsTu0-ogg-128-tc.ogg"));

		yield return new WaitForSeconds (2.0f);

		Debug.Log ("pausing sound");
		Pause ();

		yield return new WaitForSeconds (2.0f);

		Debug.Log ("resuming sound");
		Resume ();

		yield return new WaitForSeconds (2.0f);

		Debug.Log ("stopping sound");
		Stop ();

	}

	IEnumerator Play(string url) {
		var www = new WWW(url);

		var clip = www.GetAudioClip (false, /* 2D */
		                            true); /* stream and play as soon as possible */
		                            
		while (!clip.isReadyToPlay) {
			Debug.Log("loading song data..");
			yield return new WaitForSeconds(0.2f);
		}

		audioSource.clip = clip;
		audioSource.loop = false;
		audioSource.Play ();

		while (applicationPaused || audioSource.isPlaying || paused) {
			yield return true;
		}

		Debug.Log ("completed audio!");
	}

	void Pause() {
		paused = true;
		audioSource.Pause ();
	}

	void Resume() {
		paused = false;
		audioSource.Play ();
	}

	void Stop() {
		audioSource.Stop ();
	}

	
	// Update is called once per frame
	void Update () {
	
	}
}
