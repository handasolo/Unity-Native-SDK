using UnityEngine;
using System.Collections;
using SimpleJSON;

public class PopupPlayerGUI : MonoBehaviour {
	
	private Rect windowRect = new Rect (20, 20, 150, 0);
	private Player player;

	private bool validCountry = false;

	private bool displayPlayer = false;
	private bool displayingTrack = false;

	
	// Use this for initialization
	void Start () {
		Application.runInBackground = true;

		player = GetComponent<Player> ();
		
		if (Debug.isDebugBuild) {
			// DEVELOPMENT ONLY!
			Debug.Log ("resetting client id for debugging");
			player.ResetClientId ();
		}

		player.onPlacement += (p, d) => {
			// You can only get placement information if you first get a valid
			// client, which implies you are in the US.
			validCountry = true;
		};

		player.onPlayStarted += (p, d) => {
			StartCoroutine(ShowCurrentTrack());
		};
		
		player.onNotInUS += (p) => {
			// boo, we're not in the US!
			validCountry = false;
		};
		
		player.Play ();
	}
	
	void OnGUI() {
		if (validCountry) {
			if (GUI.Button(new Rect(0, 0, 100, 100), "Music")) {
				displayPlayer = !displayPlayer;
			}

			if (displayingTrack) {
				var play = player.activePlay;
	        	GUI.Label (new Rect(100, 0, Screen.width - 100, 100), 
     		            play["audio_file"]["track"]["title"] + " by " + play["audio_file"]["artist"]["name"] + " on " + play["audio_file"]["release"]["title"]);
			}

			// only display controls after we've tuned in
			if (displayPlayer) {
				var windowWidth = 300;
				var windowHeight = 180;
				var windowX = (Screen.width - windowWidth) / 2;
				var windowY = (Screen.height - windowHeight) / 2;
				
				windowRect = new Rect (windowX, windowY, windowWidth, windowHeight);
				
				windowRect = GUILayout.Window (0, windowRect, WindowFunction, player.placement["name"]);
			}
		}
	}
	
	private IEnumerator ShowCurrentTrack() {
		displayingTrack = true;

		yield return new WaitForSeconds(4.0f);

		displayingTrack = false;
	}
	
	void WindowFunction(int windowId) {
		// player is idle and user hasn't started playing anything yet
		if (player.currentState == PlayerState.Idle) {
			
			GUILayout.Label ("Tune in!");
			
			if (GUILayout.Button ("Play")) {
				player.Play ();
			}
			
			// we're playing something
		} else if (player.currentState != PlayerState.Exhausted) {
			var play = player.activePlay;
			
			GUILayout.Label (play["audio_file"]["track"]["title"] + " by " + play["audio_file"]["artist"]["name"] + " on " + play["audio_file"]["release"]["title"]);
			
			GUILayout.BeginHorizontal ();
			
			if (player.currentState == PlayerState.Paused) {
				if (GUILayout.Button ("Play")) {
					player.Play ();
				}
				
			} else if (player.currentState == PlayerState.Playing) {
				if (GUILayout.Button ("Pause")) {
					player.Pause ();
				}
				
			}
			
			GUI.enabled = player.MaybeCanSkip();
			if (GUILayout.Button ("Skip")) {
				player.RequestSkip();
			}
			GUI.enabled = true;
			
			GUILayout.EndHorizontal();
			
			// we've run out of songs
		} else { // PlayerState.Exhausted
			
			GUILayout.Label ("Sorry, there is no more music available");
			
			// you could show a play button here, so the user could try to tune
			// in again.
			
		}
	}
}
