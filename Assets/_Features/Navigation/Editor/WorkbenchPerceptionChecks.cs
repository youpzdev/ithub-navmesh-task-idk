using System;
using System.Collections.Generic;
using UnityEngine;

public static class WorkbenchPerceptionChecks
{
    [Serializable]
    public class Report
    {
        public int passed;
        public List<string> failures = new();
    }

    public static Report Check()
    {
        var report = new Report();
        var observer = new Vector3(1000, 0, 1000);
        var perception = new WorkbenchPerception(6, 90, 2, 1 << 30);
        var front = observer + Vector3.forward * 4;
        Expect(report, perception.CanSee(observer, Vector3.forward, front), "Обзор вперед: ожидалось true");
        Expect(report, !perception.CanSee(observer, Vector3.forward, observer - Vector3.forward * 4), "Обзор назад: ожидалось false");
        float radians = 45 * Mathf.Deg2Rad;
        var boundary = observer + new Vector3(Mathf.Sin(radians), 0, Mathf.Cos(radians)) * 4;
        Expect(report, perception.CanSee(observer, Vector3.forward, boundary), "Граница половины угла 45: ожидалось true");
        radians = 45.1f * Mathf.Deg2Rad;
        var outside = observer + new Vector3(Mathf.Sin(radians), 0, Mathf.Cos(radians)) * 4;
        Expect(report, !perception.CanSee(observer, Vector3.forward, outside), "За границей 45.1: ожидалось false");
        Expect(report, perception.CanSee(observer, Vector3.forward, observer + Vector3.forward * 6), "Граница дальности 6: ожидалось true");
        Expect(report, !perception.CanSee(observer, Vector3.forward, observer + Vector3.forward * 6.01f), "За дальностью 6.01: ожидалось false");
        Expect(report, !perception.CanSee(observer, Vector3.zero, front), "Нулевое направление: ожидалось false");
        Expect(report, !perception.CanSee(observer, Vector3.forward, new Vector3(float.NaN, 0, 0)), "NaN цель: ожидалось false");

        var wall = new GameObject("PerceptionCheckWall");
        wall.layer = 30;
        wall.transform.position = observer + new Vector3(0, 1, 2);
        var collider = wall.AddComponent<BoxCollider>();
        collider.size = new Vector3(4, 2, 0.3f);
        try
        {
            Physics.SyncTransforms();
            Expect(report, !perception.CanSee(observer, Vector3.forward, front), "Стена между врагом и целью: ожидалось false");
            wall.SetActive(false);
            Physics.SyncTransforms();
            Expect(report, perception.CanSee(observer, Vector3.forward, front), "Удалена преграда: ожидалось true");
            perception.Tick(observer, Vector3.forward, front, true, true, 0.1f);
            Expect(report, perception.State == EnemyAwareness.Detected, $"Обнаружение: получено {perception.State}, ожидалось Detected");
            Expect(report, perception.LastSeen == front, $"Последнее наблюдение: получено {perception.LastSeen}, ожидалось {front}");
            perception.Tick(observer, Vector3.forward, observer - Vector3.forward, true, true, 0.25f);
            Expect(report, perception.State == EnemyAwareness.Searching, $"Потеря цели: получено {perception.State}, ожидалось Searching");
            Expect(report, perception.LastSeen == front, $"Поиск не знает новую позицию: получено {perception.LastSeen}, ожидалось {front}");
            perception.Tick(observer, Vector3.forward, observer - Vector3.forward * 3, true, true, 1.75f);
            Expect(report, perception.State == EnemyAwareness.Patrol, $"Таймер поиска закончился: получено {perception.State}, ожидалось Patrol");
            Expect(report, perception.LastSeen == front, "Истечение поиска изменило LastSeen без наблюдения");
            perception.Tick(observer, Vector3.forward, front, true, true, 0.1f);
            perception.Tick(observer, Vector3.forward, front, false, true, 0);
            Expect(report, perception.State == EnemyAwareness.Patrol, $"Выключение угрозы: получено {perception.State}, ожидалось Patrol");
            perception.Tick(observer, Vector3.forward, front, true, true, 0.1f);
            perception.Tick(observer, Vector3.forward, front, true, false, 0);
            Expect(report, perception.State == EnemyAwareness.Patrol, $"Гибель цели: получено {perception.State}, ожидалось Patrol");
        }
        catch (Exception error)
        {
            report.failures.Add($"Проверка обзора: {error.GetType().Name}: {error.Message}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(wall);
            Physics.SyncTransforms();
        }

        return report;
    }

    private static void Expect(Report report, bool condition, string failure)
    {
        if (condition) report.passed++;
        else report.failures.Add(failure);
    }
}
