---
title: "Unity — запуск путешественника по своей политике"
aliases:
  - Запуск MCDA-агента в Unity
  - CorridorRisk2D — запуск
tags:
  - курс/МиИМПР
  - модуль/MCDA-агент
  - unity
  - инструкция
course: "Математические и инструментальные методы поддержки принятия решений"
module: "Агент, принимающий решения"
notebook: "MCDA_Agent_Policy_Unity_v4.ipynb"
scene: "CorridorRisk2D"
created: 2026-10-08
---

# Unity — запуск путешественника по своей политике

Путь от файла политики, посчитанного в ноутбуке, до путешественника, который ходит по
сцене `CorridorRisk2D`. Подробности и разбор ошибок — в методичке «Агент, принимающий
решения», Часть 3.

> [!info] Исходное состояние
> Ноутбук `MCDA_Agent_Policy_Unity_v4.ipynb` выполнен целиком (**Run All**), ошибок нет.

---

## Что понадобится

- [ ] **Unity Hub** и **Unity 6 (6000.0 LTS)** — установка в методичке, Часть 1.
- [ ] Архив **`CorridorRisk2D_Scripts.zip`** — новая версия с путешественником (внутри есть `WeatherZone.cs` и `SupplyPoint.cs`).
- [ ] Свой файл **`policy_unity.json`** — ноутбук сохранил его в папку `unity_export` рядом с собой.

> [!tip] Где лежит `policy_unity.json`
> Полный путь напечатан в выводе §12 ноутбука, в строке «Папка экспорта».

---

## Шаг 1. Создать проект

- [ ] Unity Hub → **New project** → шаблон **2D (URP)** (если его нет — **Universal 2D**).
- [ ] Имя проекта `CorridorRisk2D` → **Create project**.

> [!note]
> Первое открытие занимает 2–5 минут: Unity компилирует шейдеры URP.

---

## Шаг 2. Подключить скрипты

- [ ] Распаковать `CorridorRisk2D_Scripts.zip`.
- [ ] Скопировать папку `Assets` из архива **поверх** папки `Assets` проекта (через Проводник).
- [ ] Вернуться в Unity и дождаться окончания компиляции (индикатор внизу справа).
- [ ] Открыть **Window → General → Console**: красных ошибок быть не должно.
- [ ] Если Unity предложит **Import TMP Essentials** — согласиться.

> [!warning] Если в проекте лежат скрипты первой версии
> Файлы вроде `ThreatZone.cs`, `CoverZone.cs`, `Pickup.cs`, `ObservationPost.cs` — это старый
> архив. Сначала удалите в Unity папки `Assets/Scripts` и `Assets/Data` и объект `Level` со
> сцены, иначе старые и новые классы столкнутся и проект не скомпилируется.

---

## Шаг 3. Подложить свою политику

- [ ] (по желанию) Переименовать эталонный `Assets/Data/policy_unity.json` в `policy_topsis_reference.json` — чтобы потом сравнить поведение.
- [ ] Перетащить **свой** `policy_unity.json` из папки `unity_export` в `Assets/Data/` в окне Project.

---

## Шаг 4. Построить уровень

- [ ] **File → Save As** → `Assets/Scenes/CorridorRisk2D.unity`.
- [ ] Меню **CorridorRisk → Построить уровень из JSON**.

Появятся три тропы, участки непогоды и укрытий, родники, ягодники, смотровые точки,
стоянка и горный приют.

---

## Шаг 5. Ассет баланса

- [ ] Project → правой кнопкой по `Assets` → **Create → CorridorRisk → Balance**.
- [ ] Назвать `CorridorRiskBalance`. Значения по умолчанию не менять.

---

## Шаг 6. Объект Systems

- [ ] Hierarchy → **Create Empty** → назвать `Systems`.
- [ ] **Add Component → Level Graph** → `Level Json` ← `Assets/Data/level_corridor_risk_2d.json`.

> [!help]- Где находится поле `Level Json`
> 1. В окне **Hierarchy** щёлкните по объекту `Systems` — справа откроется **Inspector**.
> 2. Внизу Inspector нажмите **Add Component**, наберите `Level Graph` и выберите его.
> 3. В Inspector появится блок **Level Graph** с полем **Level Json** — сейчас в нём написано `None (Text Asset)`.
> 4. Заполните его одним из способов:
>    - перетащите мышью файл `level_corridor_risk_2d` из окна **Project** (папка `Assets/Data`) прямо в это поле;
>    - или нажмите маленький кружок справа от поля и выберите `level_corridor_risk_2d` в списке.
>
> **Level Graph не находится в Add Component?** Значит, скрипты не скомпилировались. Откройте
> **Console** и посмотрите красные ошибки; проверьте, что файл `Assets/Scripts/Level/LevelGraph.cs`
> на месте. Окна Hierarchy, Project, Inspector и Console открываются через **Window → General**.

- [ ] **Add Component → Episode Logger** → `Run Id` ← своя фамилия латиницей.
- [ ] **Add Component → Episode Manager** — поля заполняются на шаге 8.

---

## Шаг 7. Путешественник (объект Agent)

- [ ] **Create Empty** → назвать `Agent`, позиция **(−29, 0, 0)**.
- [ ] **Sprite Renderer** → Sprite ← встроенный **Knob**; Transform → Scale **(1.2, 1.2, 1)**.
- [ ] **Circle Collider 2D** → Radius `0.6`, **Is Trigger не ставить**.
- [ ] **Agent State** → `Graph` ← Systems, `Balance` ← CorridorRiskBalance.
- [ ] **Agent Motor** → `Balance` ← ассет, `State` ← Agent.
  - [ ] В появившемся **Rigidbody 2D**: Body Type = **Kinematic**, Gravity Scale = **0**.

> [!help]- Что значит «`Balance` ← ассет» и «`State` ← Agent»
> **Ассет** — это любой файл проекта в окне **Project** (скрипт, картинка, json). Здесь имеется
> в виду `CorridorRiskBalance`, созданный на шаге 5: он хранит числа баланса — скорость,
> расход сил, гистерезис, сглаживание.
>
> - Поле **Balance** в блоке Agent Motor показывает `None (Corridor Risk Balance)`. Перетащите
>   в него `CorridorRiskBalance` из окна Project или нажмите кружок справа и выберите его.
> - Если файла `CorridorRiskBalance` нет — вернитесь к шагу 5 (**Create → CorridorRisk → Balance**).
> - Поле **State** ← перетащите сам объект `Agent` из окна **Hierarchy**. Компонент ссылается
>   на объект, на котором сам находится, — это нормально: Unity возьмёт с него Agent State.
>
> Тот же ассет `CorridorRiskBalance` ставится в поле `Balance` у **Agent State**, **Agent Motor**,
> **Strategy Executor** и **Episode Manager**.
- [ ] **Agent Triggers** → `State` ← Agent.
- [ ] **Decision Policy**:
  - [ ] `Policy Asset` ← **свой** `policy_unity.json`;
  - [ ] `Stochastic When Ambiguous` — включено;
  - [ ] `Softmax Temperature` — `0.04`.
- [ ] **Trail Renderer** → Time `3`, Width `0.25`, Materials → Element 0 ← **Sprites-Default**.
- [ ] Подпись: правой кнопкой по Agent → **3D Object → Text – TextMeshPro** → назвать `Label`, позиция **(0, 1.6, 0)**, Font Size `3`.
- [ ] **Strategy Label** → `Label` ← объект Label; `Trail` и `Body` ← компоненты на Agent.

> [!help]- Как заполнить Strategy Label
> У компонента три поля:
>
> | Поле | Что поставить | Зачем |
> |---|---|---|
> | `Label` | дочерний объект `Label` | текст над головой путешественника |
> | `Trail` | Trail Renderer на `Agent` | цвет следа под выбранный вариант |
> | `Body` | Sprite Renderer на `Agent` | цвет кружка-путешественника |
>
> Порядок действий:
> 1. Выделите `Agent` в Hierarchy.
> 2. Нажмите **замок** в правом верхнем углу Inspector — он перестанет переключаться на другие объекты.
> 3. Раскройте стрелку слева от `Agent` в Hierarchy, чтобы увидеть `Label`.
> 4. Перетащите `Label` в поле **Label**.
> 5. Перетащите сам `Agent` в поле **Trail**, затем в поле **Body** — Unity сам найдёт на нём нужный компонент.
> 6. Снимите замок.
>
> Вместо перетаскивания можно нажать кружок справа от поля и выбрать объект из списка.
>
> **Label не встаёт в поле?** Скорее всего, создан **UI → Text** вместо **3D Object → Text – TextMeshPro**.
> Удалите его и создайте заново. **Agent не встаёт в Trail или Body?** На нём нет Trail Renderer
> или Sprite Renderer — проверьте пункты 2 и 8 этого шага.
- [ ] **Strategy Executor**:
  - [ ] `Graph` ← Systems;
  - [ ] `State`, `Motor`, `Policy`, `Label` ← Agent;
  - [ ] `Balance` ← CorridorRiskBalance;
  - [ ] `Logger` ← Systems;
  - [ ] `Forced Strategy` — оставить пустым.

> [!tip] Почему след розовый
> У Trail Renderer не назначен материал. Materials → Element 0 → **Sprites-Default**.

---

## Шаг 8. Достроить Episode Manager

Объект **Systems** → компонент **Episode Manager**:

| Поле | Значение |
|---|---|
| `Graph` | Systems |
| `State` | Agent |
| `Motor` | Agent |
| `Triggers` | Agent |
| `Executor` | Agent |
| `Logger` | Systems |
| `Balance` | CorridorRiskBalance |
| `Mode` | **Demo** |

---

## Шаг 9. Проверить и запустить

- [ ] Меню **CorridorRisk → Проверить сцену** — скрипт покажет, какие ссылки не заполнены.
- [ ] Когда проверка прошла без замечаний — **Play**.

> [!success] Что должно быть видно
> - путешественник появляется у стоянки слева;
> - над ним подпись варианта действий (DIRECT, WAIT…) и ключ ситуации вида `2|2|1|1|1`;
> - он идёт по одной из трёх троп и оставляет цветной след;
> - при входе в сине-серый участок непогоды подпись меняется — он пересматривает решение;
> - поход заканчивается в приюте (зелёный круг), изнеможением или таймаутом, и сразу начинается следующий.

---

## Шаг 10. Поиграть ситуацией

Прямо во время игры двигайте ползунки `Demo Energy / Dist / Weather / Supplies / Shelter`
в компоненте **Episode Manager**. Каждый следующий поход начнётся из заданной ситуации.

| Energy | Weather | Supplies | Чего ждать от эталонной политики |
|---|---|---|---|
| 0,9 | 0,2 | 0,8 | напрямик по открытому склону (центральная тропа) |
| 0,3 | 0,8 | 0,7 | переждать в укрытии или вернуться к стоянке |
| 0,8 | 0,3 | 0,2 | к родникам по нижней тропе |
| 0,7 | 0,6 | 0,7 | защищённой лесной тропой (верхняя) |

> [!question] Поведение не совпало с таблицей?
> Это не ошибка: ваши веса задают другой характер путешественника. Сверьтесь с картой
> политики в §11 ноутбука и объясните расхождение.

---

## Частые сбои

| Симптом | Причина | Что делать |
|---|---|---|
| Ошибка `TMPro` в Console | не импортированы TMP Essentials | Window → TextMeshPro → Import TMP Essential Resources |
| `NullReferenceException` при Play | не заполнена ссылка в инспекторе | CorridorRisk → Проверить сцену |
| `[LevelGraph] не назначен levelJson` | пустое поле `Level Json` | перетащить `level_corridor_risk_2d.json` |
| `[DecisionPolicy] не назначен policyAsset` | пустое поле `Policy Asset` | перетащить свой `policy_unity.json` |
| Путешественник стоит на месте | выбран WAIT — пережидает в укрытии | это нормально; смените стартовую ситуацию |
| Путешественник мечется между вариантами | мал гистерезис или сглаживание | в CorridorRiskBalance: `hysteresis` → 0,04; `smoothing` → 0,8 |
| След ярко-розовый | у Trail Renderer нет материала | Element 0 ← Sprites-Default |
| Проверка пишет `не найдено: GoalZone` (или `SafeZone`, «на агенте») | работают скрипты первой версии | удалить `ThreatZone.cs`, `CoverZone.cs`, `Pickup.cs`, `ObservationPost.cs` из `Assets/Scripts/Level`, исправить ошибки в Console, заново построить уровень |
| Нет компонента в **Add Component** | скрипты не скомпилировались | Console → исправить красные ошибки (шаг 2) |
| Поле не принимает перетаскиваемый объект | на объекте нет нужного компонента или выбран не тот тип | см. пояснения в шагах 6 и 7 |
| Inspector «убегает» при перетаскивании | выделение переключилось на другой объект | замок в правом верхнем углу Inspector |

---

## Дальше

> [!info] О какой методичке речь
> «Методичка» — файл `МЕТОДИЧКА_студентам_MCDA-агент_Unity.md` (методические указания к модулю «Агент, принимающий решения»). Если положить его в хранилище Obsidian рядом с этой заметкой, ссылки ниже станут кликабельными.

- Замер на 500 походах (режим **Benchmark**, `Time Scale` = 20) — [[МЕТОДИЧКА_студентам_MCDA-агент_Unity#Часть 4. Занятие 3 (вторая половина). Измерения|методичка, Часть 4]].
- Обучение с подкреплением в той же сцене (ML-Agents) — [[МЕТОДИЧКА_студентам_MCDA-агент_Unity#Часть 5. Занятие 4. Обучение с подкреплением (уровень B)|методичка, Часть 5]].
