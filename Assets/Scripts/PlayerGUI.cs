using UnityEngine;
using System.Collections;
using SimpleJSON;

public class PlayerGUI : MonoBehaviour {
	
	private Rect windowRect = new Rect (20, 20, 150, 0);
	private Player player;


	private string[] stationTitles;
	private int stationIndex;

	private bool displayPlayer = false;
	
	// Use this for initialization
	void Start () {
		Application.runInBackground = true;

		player = GetComponent<Player> ();

		if (Debug.isDebugBuild) {
			// DEVELOPMENT ONLY!
			Debug.Log ("resetting client id for debugging");
			player.resetClientId ();
		}

		player.onStations += (p, stations) => {
			JSONArray sa = stations.AsArray;

			stationTitles = new string[sa.Count];

			for (int i = 0; i < sa.Count; i++) {
				stationTitles[i] = sa[i]["name"];

				if (player.stationId == (string) sa[i]["id"]) {
					stationIndex = i;
				}
			}
		};

		player.onStationChanged += (p, stationId) => {
			if (player.stations == null) {
				return;
			}

			JSONArray sa = player.stations.AsArray;
			
			for (int i = 0; i < sa.Count; i++) {
				if (player.stationId == (string) sa[i]["id"]) {
					stationIndex = i;
					return;
				}
			}
		};

		player.onClientRegistered += (p) => {
			// huzzah, we're in the US!
			displayPlayer = true;
		};

		player.onNotInUS += (p) => {
			// boo, we're not in the US!
			displayPlayer = false;
		};

		player.Tune ();
	}
		
	void OnGUI() {
		// only display controls after we've tuned in
		if (displayPlayer && (player.placement != null)) {
			var windowWidth = 300;
			var windowHeight = 0;
			var windowX = (Screen.width - windowWidth) / 2;
			var windowY = (Screen.height - windowHeight) / 2;
			
			windowRect = new Rect (windowX, windowY, windowWidth, windowHeight);
			
			windowRect = GUILayout.Window (0, windowRect, WindowFunction, player.placement["name"], 
			                               new GUILayoutOption[] { GUILayout.ExpandHeight(true), GUILayout.MinHeight(20) });

		}
	}
	
	void WindowFunction(int windowId) {
		switch (player.currentState) {

		// player is idle and user hasn't started playing anything yet
		case PlayerState.Idle:
			GUILayout.Label ("Tune in to " + player.station["name"]);
			
			if (GUILayout.Button ("Play")) {
				player.Play ();
			}
			break;

		// waiting for response from server to start music playback
		case PlayerState.Tuning:
			GUILayout.Label ("Tuning in to " + player.station["name"]);
			
			break;

		// ran out of music in the current station
		case PlayerState.Exhausted:
			GUILayout.Label ("Sorry, there is no more music available in this station");
			
			// you could show a play button here, so the user could try to tune
			// in again.
			break;

		// music has started streaming
		case PlayerState.Playing:
		case PlayerState.Paused:
			var play = player.activePlay;
			
			if (play != null) {
				GUILayout.Label (play["audio_file"]["track"]["title"] + " by " + play["audio_file"]["artist"]["name"] + " on " + play["audio_file"]["release"]["title"]);
			}
			
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

			break;
		}

		// display available stations
		if (player.stations.Count > 1) {
			GUILayout.Space (20);
			GUILayout.Label ("Try one of our fabulous stations");

			var newStationIndex = GUILayout.SelectionGrid (stationIndex, stationTitles, 1);
			if (newStationIndex != stationIndex) {
				bool isIdle = player.currentState == PlayerState.Idle;

				player.stationId = player.stations[newStationIndex]["id"];

				if (!isIdle) {
					player.Play();
				}
			}
		}

	}
	
}
