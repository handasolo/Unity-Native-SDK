using UnityEngine;
using System.Collections;
using SimpleJSON;
using FeedFm;

public class PlayerGUI : MonoBehaviour {
	
	private Rect windowRect;
	private Player player;
	
	private string[] stationTitles;
	private int stationIndex;

	void Awake() {
		player = GetComponent<Player>();
	}

	// Use this for initialization
	void Start () {
		Application.runInBackground = true;

		if (Debug.isDebugBuild) {
			// DEVELOPMENT ONLY!
			Debug.Log ("resetting client id for debugging");
			player.ResetClientId ();
		}

		// map station list to array of station names
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

		// figure out the index of the new station in the list of stations
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

		var windowWidth = 300;
		var windowHeight = 200;
		var windowX = (Screen.width - windowWidth) / 2;
		var windowY =  50;
		
		windowRect = new Rect (windowX, windowY, windowWidth, windowHeight);

		player.Tune ();
	}

	void OnGUI() {
		// only display controls if we're in the US
		if (player.inUS && (player.placement != null)) {
			windowRect = GUILayout.Window (0, windowRect, WindowFunction, player.placement["name"], 
			                               new GUILayoutOption[] { GUILayout.ExpandHeight(true), GUILayout.MinHeight(20) });
		}
	}
	
	void WindowFunction(int windowId) {
		switch (player.currentState) {

		// player is idle and user hasn't started playing anything yet
		case PlayerState.Idle:
			GUILayout.Label ("Tune in to " + player.station["name"]);
			
			if (GUILayout.Button ("Play", GUILayout.Height (50))) {
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
				if (GUILayout.Button ("Play", GUILayout.Height (50))) {
					player.Play ();
				}
				
			} else if (player.currentState == PlayerState.Playing) {
				if (GUILayout.Button ("Pause", GUILayout.Height (50))) {
					player.Pause ();
				}

			}

			GUI.enabled = player.MaybeCanSkip();
			if (GUILayout.Button ("Skip", GUILayout.Height (50))) {
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

			var newStationIndex = GUILayout.SelectionGrid (stationIndex, stationTitles, 1, GUILayout.Height (50 * player.stations.Count));
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
