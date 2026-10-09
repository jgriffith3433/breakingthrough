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
        if (AbleToLockOnCrosshair != null) {
            SpriteRenderer marker = AbleToLockOnCrosshair.GetComponent<SpriteRenderer>();
            if (marker != null) {
                marker.enabled = false;
            }
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

    public GameObject Neighbor(GameObject current, int side) {
        if (ZeldaCam == null || current == null) {
            return null;
        }

        Vector3 right = ZeldaCam.transform.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.001f) {
            return null;
        }
        right.Normalize();

        GameObject bestObject = null;
        float bestScore = 0.15f;
        foreach (GameObject candidate in NearObjects) {
            if (candidate == null || candidate == current) {
                continue;
            }
            Vector3 dir = candidate.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) {
                continue;
            }
            float score = Vector3.Dot(dir.normalized, right) * side;
            if (score > bestScore) {
                bestScore = score;
                bestObject = candidate;
            }
        }
        return bestObject;
    }
}

