using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LockOn : MonoBehaviour {
    public ZeldaStyleCamera ZeldaCam;
    private List<GameObject> NearObjects = new List<GameObject>();
    public GameObject NearestObject;
    [SerializeField]
    private GameObject AbleToLockOnCrosshair;

    void Start() {
        ZeldaCam = GameObject.FindWithTag("MainCamera").GetComponent<ZeldaStyleCamera>();
    }

    void Update() {
        if (NearObjects.Count == 0) {
            NearestObject = null;
        }
        foreach(var nearObject in NearObjects) {
            if (NearestObject == null) {
                NearestObject = nearObject;
            }
            if ((transform.position - nearObject.transform.position).magnitude <
                (transform.position - NearestObject.transform.position).magnitude) {
                NearestObject = nearObject;
            }
        }
        if (NearestObject != null && ZeldaCam.LockedOnGameObject == null) {
            AbleToLockOnCrosshair.GetComponent<SpriteRenderer>().enabled = true;
            AbleToLockOnCrosshair.transform.position = NearestObject.transform.position;
            AbleToLockOnCrosshair.transform.LookAt(ZeldaCam.transform.position);
        } else {
            AbleToLockOnCrosshair.GetComponent<SpriteRenderer>().enabled = false;
        }
    }

    void OnTriggerEnter(Collider col) {
        if (!NearObjects.Contains(col.gameObject)) {
            if (col.gameObject.CompareTag("CanLockOn") || col.gameObject.CompareTag("enemy")) {
                NearObjects.Add(col.gameObject);
            }
        }
    }

    void OnTriggerExit(Collider col) {
        NearObjects.Remove(col.gameObject);
    }
}

