using UnityEngine;

public class AppleTreeSpawner : MonoBehaviour
{
    [SerializeField] private GameObject applePrefab;
    [SerializeField] private float spawnInterval = 0.1f;
    [SerializeField] private int applesPerSpawn = 3;
    [SerializeField] private int spawnPointCount = 5;
    [SerializeField] private float spawnPointRadius = 3f;
    [SerializeField] private float appleLifetime = 5f;
    [SerializeField] private float minLateralSpeed = 1f;
    [SerializeField] private float maxLateralSpeed = 4f;
    [SerializeField] private float minDownSpeed = 1f;
    [SerializeField] private float maxDownSpeed = 5f;
    [SerializeField] private float spawnHeightOffset = 2f;

    private float _timer;

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= spawnInterval) {
            _timer = 0f;
            for (int i = 0; i < applesPerSpawn; i++) SpawnApple();
        }

        // Destroy apples that have lived long enough.
        for (int i = _spawnedApples.Count - 1; i >= 0; i--) {
            if (_spawnedApples[i] == null) {
                _spawnedApples.RemoveAt(i);
                _spawnTimes.RemoveAt(i);
            } else if (Time.time - _spawnTimes[i] >= appleLifetime) {
                Destroy(_spawnedApples[i]);
                _spawnedApples.RemoveAt(i);
                _spawnTimes.RemoveAt(i);
            }
        }
    }

    private readonly System.Collections.Generic.List<GameObject> _spawnedApples =
        new System.Collections.Generic.List<GameObject>();
    private readonly System.Collections.Generic.List<float> _spawnTimes =
        new System.Collections.Generic.List<float>();

    private void SpawnApple()
    {
        if (applePrefab == null) return;

        // Pick a random spawn point from a circle of candidates on the XZ plane.
        float angle = Random.Range(0f, Mathf.PI * 2f);
        if (spawnPointCount > 1)
            angle = (Mathf.Round(Random.Range(0, spawnPointCount)) / spawnPointCount) * Mathf.PI * 2f;
        float r = spawnPointRadius;
        Vector3 spawnPos = transform.position + new Vector3(Mathf.Cos(angle) * r, spawnHeightOffset, Mathf.Sin(angle) * r);

        GameObject apple = Instantiate(applePrefab, spawnPos, Random.rotation);

        // Prevent apples from colliding with each other.
        Collider newCol = apple.GetComponent<Collider>();
        if (newCol != null) {
            foreach (GameObject existing in _spawnedApples) {
                if (existing == null) continue;
                Collider existingCol = existing.GetComponent<Collider>();
                if (existingCol != null)
                    Physics.IgnoreCollision(newCol, existingCol, true);
            }
        }

        _spawnedApples.Add(apple);
        _spawnTimes.Add(Time.time);

        Rigidbody rb = apple.GetComponent<Rigidbody>();
        if (rb != null) {
            Vector2 lateral = Random.insideUnitCircle.normalized * Random.Range(minLateralSpeed, maxLateralSpeed);
            float downSpeed = Random.Range(minDownSpeed, maxDownSpeed);
            rb.velocity = new Vector3(lateral.x, -downSpeed, lateral.y);
        }
    }

    private void OnDestroy()
    {
        foreach (GameObject apple in _spawnedApples) {
            if (apple != null) Destroy(apple);
        }
    }
}
