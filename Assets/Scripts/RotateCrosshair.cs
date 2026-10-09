using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateCrosshair : MonoBehaviour {
    [SerializeField]
    private GameObject LockedOnCrosshair;
    private void Update() {
        LockedOnCrosshair.transform.Rotate(new Vector3(0, 0, 1), 1);
    }
}
