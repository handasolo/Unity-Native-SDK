#pragma strict

var speed:float = 10.0; // how fast we're going

function Start () {
	Debug.Log("starting on game object " + gameObject.name);
}


function Update () {
	//Debug.Log("updating game object " + Time.time);
	transform.Rotate(Vector3.up*speed*Time.deltaTime);
	//iTween.RotateBy(gameObject, iTween.Hash("y", 1, "tme", 2, "easeType", "easeInOutBack", "loopType", "pingPong"));
}