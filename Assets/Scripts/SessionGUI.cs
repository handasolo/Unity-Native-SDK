using UnityEngine;
using System.Collections;

public class SessionGUI : MonoBehaviour {

	private Rect windowRect = new Rect (20, 20, 150, 0);
	private string display = "Welcome to the Session tester";
	private Session session;

	// Use this for initialization
	void Start () {
		session = GetComponent<Session> ();
		session.ResetClientId ();

		session.onPlacement += (p, d) => Debug.Log("updated placement!");
		session.onStations += (p, d) => Debug.Log ("updated station list");
		session.onNotInUS += (p) => Debug.Log ("not in the US, sorry");
		session.onPlacementChanged += (p, d) => Debug.Log ("placement was changed");
		session.onStationChanged += (p, d) => Debug.Log ("station changed");
		session.onPlayActive += (p, d) => {
			Debug.Log ("play is now active");
			display = d ["audio_file"] ["track"] ["title"] + " by " + d ["audio_file"] ["artist"] ["name"] + " is active";
		};
		session.onPlayStarted += (p, d) => {
			Debug.Log ("play has started");
			display = d ["audio_file"] ["track"] ["title"] + " by " + d ["audio_file"] ["artist"] ["name"] + " is playing";
		};
		session.onPlayCompleted += (p, d) => {
			Debug.Log ("play is complete");
			display = "";
		};
		session.onPlaysExhausted += (p) => {
			Debug.Log ("plays are exhausted");
			display = "Out of music!";
		};
		session.onSkipDenied += (p) => Debug.Log ("skip denied!");
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
			session.Tune ();
		}

		if (GUILayout.Button ("Start")) {
			session.ReportPlayStarted ();
		}

		if (GUILayout.Button ("Elapse")) {
			session.ReportPlayElapsed(20);
		}

		if (GUILayout.Button ("Complete")) {
			session.ReportPlayCompleted();
		}

		if (GUILayout.Button ("Skip")) {
			session.RequestSkip ();
		}

		if (GUILayout.Button ("Invalidate")) {
			session.RequestInvalidate();
		}

		GUILayout.EndHorizontal ();
	}
	
	// Update is called once per frame
	void Update () {
	
	}
}
