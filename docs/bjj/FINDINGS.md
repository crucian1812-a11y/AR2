# Аудит: что реально есть в аккаунте

Проверено 13.08.2026. Просмотрены все 8 репозиториев аккаунта
`crucian1812-a11y` и **все ветки в каждом** (`ar`, `AR2`, `ARALL`, `2`,
`game`, `new2`, `nnnnnn`, `hotel-rate-watch.`) — по спискам файлов и по
содержимому workflow-файлов.

## 1. Unity: не найдено

Ни в одном репозитории и ни в одной ветке нет:

- папки `Assets/` или `ProjectSettings/` (обязательные для Unity-проекта),
- файла `ProjectVersion.txt`, `.asmdef`, сцен `.unity`,
- ни одного workflow с `game-ci/unity-builder`,
- ни одной ссылки на секреты `UNITY_LICENSE` / `UNITY_EMAIL` / `UNITY_SERIAL`.

Значения секретов GitHub не может прочитать никто, включая API — это by design.
Но **их использование** видно в workflow-файлах, и там Unity не упоминается
нигде. Если секреты и заведены в настройках репозитория, ими ещё ни разу
ничего не собиралось.

## 2. Что собиралось на самом деле

APK в этом аккаунте собирались, и не раз — но **на Godot и на нативном
Android**, а не на Unity.

| Проект | Где | Стек | Сборка |
|---|---|---|---|
| **Vityazi 3D** | `ar` → `claude/dreamy-maxwell-ixkimb`, папка `vityazi3d/` | **Godot 4.3**, GDScript, `.tscn` | `.github/workflows/godot.yml` |
| **Vityazi** | там же, `vityazi/` | Kotlin + Canvas | `.github/workflows/game.yml` |
| **BookTracker** | там же, `booktracker/` | Kotlin + Compose + Room | `.github/workflows/android.yml` |
| **Warforge** | `game` → `claude/apk-file-upload-tji1t2` | JS/WebGL в WebView | `.github/workflows/warforge-android.yml` |

Готовые APK лежат в ветках `ar/apk-latest` (`vityazi3d.apk`) и
`ar/booktracker-apk-latest` (`booktracker.apk`).

### Ключевой момент: секреты для сборки APK не нужны вообще

Во всех четырёх пайплайнах ключ подписи **генерируется прямо в раннере**:

```yaml
- name: Generate debug keystore
  run: |
    keytool -keyalg RSA -genkeypair -alias androiddebugkey \
      -keypass android -keystore "$PWD/debug.keystore" \
      -storepass android -dname "CN=Android Debug,O=Android,C=US" \
      -validity 10000 -deststoretype pkcs12
```

Debug-подпись достаточна для установки на телефон сайдлоадом. Реальный
upload-key нужен только для публикации в Google Play. То есть ни один секрет
в репозитории для нашей задачи не требуется — **кроме случая, если движком
будет Unity**.

### Godot-пайплайн уже отлажен

`godot.yml` из `vityazi3d` — рабочий и полный: JDK 17, Android SDK,
build-tools 34, headless Godot + export templates, debug keystore, экспорт
APK. Его можно взять почти дословно.

## 3. Что это значит для выбора движка

| | Unity | Godot 4.3 |
|---|---|---|
| Лицензия для CI | нужна (Personal бесплатна, но нужна активация: `UNITY_EMAIL`, `UNITY_PASSWORD`, `UNITY_LICENSE`) | не нужна |
| Пайплайн | писать с нуля | **уже работает в `ar`** |
| Docker-образ в CI | ~10 ГБ, первая сборка 20–40 мин | ~100 МБ, сборка 3–6 мин |
| Формат сцен | YAML с GUID-ссылками, руками почти не пишется | `.tscn` — читаемый текст, пишется руками свободно |
| Можно ли делать проект без редактора на машине | тяжело | **да** |
| Рендер на мобиле | URP, зрелый | Forward+ / Mobile, PBR есть, слабее по инструментам |
| Ассеты Quaternius | FBX | **glTF/GLB — родной формат** |

Решающий довод: в этой сессии у меня **нет Unity Editor** (и Android SDK тоже
нет), собирать можно только в GitHub Actions. Для Unity это значит, что каждую
сцену и префаб придётся писать вслепую в YAML с ручной расстановкой GUID, а
проверять — 30-минутной сборкой в CI. Для Godot `.tscn` — обычный текстовый
формат, который правится напрямую, а сборка занимает 5 минут.
