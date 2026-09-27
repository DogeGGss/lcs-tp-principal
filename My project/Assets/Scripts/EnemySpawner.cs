using System.Collections;
using UnityEngine;

// Generador de enemigos para el modo de práctica: cada "spawnInterval" segundos aparece un enemigo
// en uno de los puntos de spawn (elegido al azar), de forma indefinida.
public class EnemySpawner : MonoBehaviour
{
    [Tooltip("Prefab del enemigo (necesita HealthSystem y EnemyChaser).")]
    public GameObject enemyPrefab;

    [Tooltip("Puntos donde pueden aparecer los enemigos. Podés poner 2, 3 o los que quieras.")]
    public Transform[] spawnPoints;

    [Tooltip("Segundos entre cada aparición.")]
    public float spawnInterval = 5f;

    [Tooltip("Si está prendido, arranca a generar enemigos apenas empieza la escena.")]
    public bool spawnOnStart = true;

    private Coroutine routine;

    private void Start()
    {
        if (spawnOnStart) StartSpawning();
    }

    public void StartSpawning()
    {
        if (routine != null) return;
        routine = StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        if (routine == null) return;
        StopCoroutine(routine);
        routine = null;
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);
            SpawnOne();
        }
    }

    private void SpawnOne()
    {
        if (enemyPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("EnemySpawner: falta asignar el prefab o los puntos de spawn.");
            return;
        }

        Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
        Instantiate(enemyPrefab, point.position, point.rotation);
    }
}
