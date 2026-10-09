# Gaussian Splatting DX11 für VRChat PC

Experimenteller, texturbasierter Gaussian-Renderer von **Prempi**, mit Kovarianzprojektion nach [Aras Pranckevičius' UnityGaussianSplatting](https://github.com/aras-p/UnityGaussianSplatting). Ziel: **Direct3D 11, Unity Built-in Render Pipeline und VRChat Worlds/UdonSharp**. Die GPU-Sortierung läuft als Fragmentshader über `VRCGraphics.Blit`, ohne Compute-Shader und ohne gewöhnliche C#-Laufzeit.

[English documentation](README.md) · [Datenformat und Einrichtung](DATA_LAYOUT.md) · [MIT-Lizenz](LICENSE.md) · [Urheber und Fremdhinweise](THIRD_PARTY_NOTICES.md)

## Prüfstand

Prempi hat am **9. Oktober 2026** bestätigt, dass der aktuelle integrierte Renderer im **VRChat-Client funktioniert**. Mit **`passesPerFrame = 32`** am Controller war das Nachziehen nahezu nicht mehr wahrnehmbar (geschätzt etwa 0,1 Sekunden); die gemeldete Bildrate lag bei ungefähr **72 FPS**. Zuvor waren es bei 8 Durchläufen ungefähr 0,8 Sekunden und rund 40 FPS. **Virtual Desktop war auf 72 FPS begrenzt**: Die 72 sind die erreichte gemeldete Obergrenze, kein unlimitierter Maximalwert und kein Nachweis weiterer Leistungsreserve. Prempi schaute dabei durch Fenster; die Anzahl sichtbarer/gezeichneter Splats wurde nicht gemessen. Daraus folgt keine Leistungszusage für die gesamte sichtbare Stadt.

Das sind Beobachtungen auf Prempis System, kein kontrollierter A/B-Benchmark und keine instrumentierte Latenzmessung. Die höhere Bildrate kann nicht eindeutig der Einstellung zugeschrieben werden; Auflösung, Client-Bedingungen, Synchronisierung und andere Faktoren waren nicht kontrolliert. Client-Version, Headset-/Stereo-Details und genaue GPU-Framezeit sind nicht protokolliert. Ein unabhängiger Neuimport dieser reinen Quellcode-Veröffentlichung wurde nicht getestet.

Die drei Kernquellen sind unveränderte Kopien des visuell freigegebenen Stands. Die Editor-Prüfung unter **Unity 2022.3.22f1 / DX11 / NVIDIA GeForce RTX 5070 Ti** verwendete **500.000 Splats**, aufgefüllt auf **524.288**. In drei Blickrichtungen gab es keine fehlenden Einträge, keine Sortierfehler und keine Shader-Diagnosen. Die integrierten Udon-Programme kompilierten. Bereinigte Messdaten: [VALIDATION.json](VALIDATION.json).

**Die dort aufgeführten 12–51 ms enthalten Sortierung und synchrones GPU-Rücklesen im Editor. Sie sind keine reine GPU-Zeit, keine Kosten pro Frame und kein VRChat-FPS-Benchmark.** Ein belastbares Leistungsbudget für VRChat ist noch nicht bestimmt.

## Warum das Nachziehen verzögert ist

Bei 524.288 Einträgen sind **190 vollständige Vergleichsdurchläufe** nötig. **Für den erfolgreich getesteten Stand am Controller `passesPerFrame = 32` einstellen.** Das verteilt die Sortierung auf etwa sechs Update-Frames, zusätzlich zu Initialisierung, Veröffentlichung und Scheduling. Bei 72 Update-Frames pro Sekunde entsprechen sechs Frames ungefähr 83 ms. Das ist eine Rechnung, keine gemessene Gesamtlatenz.

Der unveränderte Quellcode enthält weiterhin den ursprünglichen Initialwert 8. Damit dauert ein Lauf etwa 24 Update-Frames und kann deutlich nachziehen. Deshalb vor dem Kompilieren/Build im Inspector ausdrücklich 32 zuweisen.

Der Renderer zeigt erst eine vollständig fertig sortierte Reihenfolge. Bis dahin bleibt die vorige gültige Reihenfolge sichtbar; schnelle Blickwechsel können Transparenzfehler zeigen. Während der Berechnung wird die anfangs erfasste Blickrichtung verwendet. Bei weiterer Bewegung kann danach eine weitere Sortierung nötig werden. Mehr Durchläufe pro Frame verkürzen die Wartezeit, erhöhen aber die Last pro Frame. Die gesamte Arbeit je Sortierung bleibt gleich. Der Wert ist auf 1–32 begrenzt. Der erfolgreiche Client-Test verwendet 32; verändert wurde nur die Instanzeinstellung, Shader und Daten blieben erhalten.

Auch das Zeichnen verbraucht GPU-Zeit, besonders bei großen, überlappenden transparenten Splats. Anzahl, Auflösung, Überdeckung, GPU, Avatare und die übrige Welt bestimmen die Kosten. Jeder Besucher sortiert lokal; diese Darstellung muss nicht über das Netzwerk synchronisiert werden.

## Installation

Dies ist der **Quellcode-Kern**, kein fertiges Weltpaket und kein vollständiger allgemeiner Importer. Datensätze und erzeugte Assets sind ausgeschlossen. Benötigt werden vier vorbereitete Datentexturen, native Punktmeshes, Zeichenmaterialien und drei Rendertexturen gemäß [DATA_LAYOUT.md](DATA_LAYOUT.md). Ein ursprüngliches `GaussianSplatAsset` kann nicht direkt zugewiesen werden.

1. Ein VRChat-PC-Worlds-Projekt mit SDK-unterstützter Unity-Version, Built-in-Pipeline und UdonSharp verwenden. Das SDK über den offiziellen [Creator Companion](https://vcc.docs.vrchat.com/) installieren.
2. `Runtime`, `Shaders` und `Editor` einschließlich `.meta` nach `Assets/PrempiGaussianDX11` kopieren. Dateinamen und GUIDs erhalten. Keine zweite Kopie von `NeonQuestGaussian` parallel installieren.
3. Eigene, zur Nutzung berechtigte Daten vorbereiten, alle Felder gemäß Einrichtung zuweisen, am Controller **Passes Per Frame = 32** einstellen und UdonSharp im Zielprojekt kompilieren. Erzeugte Udon-Programme gehören zum Zielprojekt.
4. Für eine Scene-View-Vorschau den Controller auswählen und **Tools → Prempi Gaussian DX11 → Preview selected cloud** verwenden. Über das Nachbarmenü stoppen. Die Vorschau nutzt temporäre Materialien und stellt Originale vor Szenenspeicherung, Reload und Playmode wieder her. Die synchrone Editor-Sortierung kann kurz blockieren.
5. Mit dem Worlds-SDK im tatsächlichen VRChat-Client auf den Zielsystemen prüfen. Der separate Neuimport dieses Quellpakets ist noch nicht unabhängig getestet.

## Grenzen

- PC/DX11; Android/Quest, DX12, Vulkan, URP und HDRP sind hier nicht validiert. Geometry-Shader werden benötigt.
- Festes RGB, keine Auswertung höherer sphärischer Harmonischer (SH). Daten mit blickabhängigen SH-Farben verlieren diesen Anteil.
- Eine Sortierreihenfolge nach der Kopf-Blickrichtung pro Besucher. Spiegel, Handkamera, parallele Kameras und Stereo-Sonderfälle sind nicht gesondert validiert.
- Statische Daten und Cloud-Transformation angenommen. Eine Cloud-Drehung/Skalierung bei unverändertem Blick löst keine eigene Neusortierung aus.
- Andere transparente Objekte werden nicht automatisch korrekt mit den Splats verschachtelt; der Shader schreibt keine Tiefe.
- Echte Cloud-Bounds, Punkt-Topologie und aufsteigende Material-Queues sind erforderlich.

## Umfang und Lizenz

Controller, zwei Shader, optionale Editor-Vorschau, Unity-Textmetadaten, Dokumentation und SHA-256-Dateiliste. Keine Beispieldaten, Weltdateien, privaten Kontaktdaten, Zugangsdaten, Projektkonfigurationen, Logs, Bilder, Binärdateien oder SDK-Inhalte. Kernquellen unverändert aus dem freigegebenen Stand kopiert.

**MIT**, ursprüngliches Copyright von Aras erhalten, Anpassungen von Prempi, entwickelt mit ChatGPT/Codex-Unterstützung. Andere VRChat-Lösungen existieren bereits, etwa [MichaelMoroz/VRChatGaussianSplatting](https://github.com/MichaelMoroz/VRChatGaussianSplatting). Ein allgemeiner Qualitäts- oder Leistungsvergleich wurde nicht nachgewiesen. Unabhängige Veröffentlichung ohne offizielle VRChat-Zusage oder Behauptung einer weltweit ersten DX11-Implementierung. Rechte an importierten Datensätzen sind gesondert zu prüfen. Details: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
