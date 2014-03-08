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
		
		yield return new WaitForSeconds (1.5f);

		Debug.Log ("playing sound");
		StartCoroutine(Play ("http://ill.com/song.ogg"));

		yield return new WaitForSeconds (1.5f);

		Debug.Log ("pausing sound");
		Pause ();

		yield return new WaitForSeconds (1.5f);

		Debug.Log ("resuming sound");
		Resume ();

		yield return new WaitForSeconds (1.5f);

		Debug.Log ("stopping sound");
		Stop ();

	}

	IEnumerator Play(string url) {
		var www = new WWW(url);

		var clip = www.GetAudioClip (false, /* false = 2D */
		                            true); /* true = stream and play as soon as possible */
		                            
		/* when streaming, we don't know the duration of songs. this is a problem. */

		while (!clip.isReadyToPlay) {
			Debug.Log("loading song data..");
			yield return new WaitForSeconds(0.2f);
		}

		Debug.Log ("done waiting to start");

		audioSource.clip = clip;
		audioSource.loop = false;
		paused = false;

		audioSource.Play ();

		Debug.Log ("length is " + clip.length);

		while (applicationPaused || audioSource.isPlaying || paused) {
			Debug.Log ("values are " + applicationPaused + ", " + audioSource.isPlaying + ", " + paused);
			Debug.Log ("elapsed is " + audioSource.time + ", " + clip.length);
			yield return true;
		}

		Debug.Log ("completed audio!");
	}

	void Pause() {
		Debug.Log ("pausing");
		paused = true;
		audioSource.Pause ();
	}

	void Resume() {
		Debug.Log ("resuming");
		paused = false;
		audioSource.Play ();
	}

	void Stop() {
		Debug.Log ("stopping!");
		audioSource.Stop ();
	}

	
	// Update is called once per frame
	void Update () {
	
	}
}
