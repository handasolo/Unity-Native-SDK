using UnityEngine;
using System.Collections;

public class PlayerGUI : MonoBehaviour {
	
	private Rect windowRect = new Rect (20, 20, 150, 0);
	private string display = "Welcome to the Player tester";
	private Player player;
	
	// Use this for initialization
	void Start () {
		player = GetComponent<Player> ();
		player.resetClientId ();
		
		player.onPlacement += (p, d) => Debug.Log("updated placement!");
		player.onStations += (p, d) => Debug.Log ("updated station list");
		player.onNotInUS += (p) => Debug.Log ("not in the US, sorry");
		player.onPlacementChanged += (p, d) => Debug.Log ("placement was changed");
		player.onStationChanged += (p, d) => Debug.Log ("station changed");
		player.onPlayActive += (p, d) => {
			Debug.Log ("play is now active");
			display = d ["audio_file"] ["track"] ["title"] + " by " + d ["audio_file"] ["artist"] ["name"] + " is active";
		};
		player.onPlayStarted += (p, d) => {
			Debug.Log ("play has started");
			display = d ["audio_file"] ["track"] ["title"] + " by " + d ["audio_file"] ["artist"] ["name"] + " is playing";
		};
		player.onPlayPaused += (p) => {
			Debug.Log ("play has paused");
		};
		player.onPlayResumed += (p) => {
			Debug.Log ("play has resumed");
		};
		player.onPlayCompleted += (p, d) => {
			Debug.Log ("play is complete");
			display = "";
		};
		player.onPlaysExhausted += (p) => {
			Debug.Log ("plays are exhausted");
			display = "Out of music!";
		};
		player.onSkipDenied += (p) => Debug.Log ("skip denied!");
	}
	
	
	void OnGUI() {
		var windowWidth = 300;
		var windowHeight = 180;
		var windowX = (Screen.width - windowWidth) / 2;
		var windowY = (Screen.height - windowHeight) / 2;
		
		windowRect = new Rect (windowX, windowY, windowWidth, windowHeight);
		
		windowRect = GUILayout.Window (0, windowRect, WindowFunction, "Draggable Window");
	}
	
	void WindowFunction(int windowId) {
		GUILayout.Label (display);
		
		GUILayout.BeginHorizontal ();
		
		if (GUILayout.Button ("Tune")) {
			player.Tune ();
		}
		
		if (GUILayout.Button ("Play")) {
			player.Play ();
		}
		
		if (GUILayout.Button ("Pause")) {
			player.Pause ();
		}
		
		if (GUILayout.Button ("Skip")) {
			player.Skip();
		}
		
		GUILayout.EndHorizontal ();
	}
	
}
