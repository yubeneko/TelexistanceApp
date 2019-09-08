using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class URLDropDown : MonoBehaviour {
	public InputField field;

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	
	}

	public void UpdateURL() {
		Dropdown dropdown = GetComponent<Dropdown> ();
		Debug.Log ("dropdown.itemText.text:" + dropdown.options [dropdown.value].text);
		string[] choice = dropdown.options [dropdown.value].text.Split(',');
		field.text = (choice.Length > 1) ? choice [1] : "";
	}
}
