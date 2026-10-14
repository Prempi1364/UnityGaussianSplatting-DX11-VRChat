# Gaussian Splatting DX11 für VRChat PC

Experimentelles, texturbasiertes Gaussian Splatting mit Kovarianzprojektion nach [Aras Pranckevičius' UnityGaussianSplatting](https://github.com/aras-p/UnityGaussianSplatting). Ziel: **Direct3D 11, Unity Built-in Render Pipeline und VRChat Worlds/UdonSharp**. Die GPU-Sortierung läuft als Fragmentshader über `VRCGraphics.Blit`, ohne Compute-Shader und ohne gewöhnliche C#-Laufzeit.

[English documentation](README.md) · [Datenformat und Einrichtung](DATA_LAYOUT.md) · [MIT-Lizenz](LICENSE.md) · [Urheber und Fremdhinweise](THIRD_PARTY_NOTICES.md)

## Quellcode-Update — 14. Oktober 2026 (Drei-Schritt-Sortierung)

Der Umfang bleibt der Renderer-Kern: Controller, RGB-Zeichenshader, Sortiershader,
Editorvorschau und Schutz der Startsortierung. Importer, optionaler SH-Zeichenshader,
Datensätze und erzeugte Unity-/Udon-Assets gehören nicht zu diesem Quellpaket.

Der Controller verwendet jetzt die Materialien der tatsächlich zeichnenden
`Gaussian draw N`-Renderer. Eine korrekt berechnete Reihenfolge wird damit direkt
von den sichtbaren Meshes verwendet. Die Sortierung erfolgt nach Blicktiefe;
Standard und Obergrenze sind **32 Durchläufe pro Frame**. Die fertige Reihenfolge
wird über Texeladressen kopiert. Der Zeichenshader projiziert die Kovarianz nach
der Augeninitialisierung im Vertex-Shader und überspringt Zentren hinter der Kamera.

Die Editorvorschau verwaltet je Kamera eine eigene Reihenfolge. Temporäre Materialien
werden vor Play zurückgesetzt und während des gesamten Play-Übergangs und Play-Modus
nicht erneut erzeugt. Der Speicherschutz setzt nach Play und vor dem Speichern die
dauerhafte Startsortierung zurück, statt flüchtige GPU-Daten zu speichern.

Der integrierte Laufzeitpfad wurde mit 2.276.102 Splats und 139 Teilmeshes geprüft:
sechs Blickrichtungen der tatsächlichen ClientSim-Game-Kamera mit normaler
Kopfverfolgung und Physik sowie wiederholte Play-Starts. Alle sichtbaren Renderer
verwendeten die fertige Sortierung. Vollständiges GPU-Rücklesen prüfte außerdem
eindeutige Punktnummern und passende Positions-/Tiefenschlüssel. Diese Integration
nutzt einen separaten SH-Zeichenshader außerhalb dieses Quellpakets.
Die aktuelle Integration wurde im VRChat-Client als funktionierend und mit
deutlich kürzerem Nachziehen bestätigt. Das ist eine qualitative Rückmeldung;
VRChat-FPS und die Gesamtlatenz im Headset wurden nicht instrumentiert gemessen. Ein frischer Import des separaten Quellpakets
muss weiterhin unabhängig geprüft werden.

### Schnellere Sortierung bei gleichem Budget

Controller und Editorvorschau fassen jetzt **bis zu drei aufeinanderfolgende
Bitonic-Vergleichsschritte in einem GPU-Durchlauf** zusammen. Die stabile Sortierung
nach Tiefenschlüssel und Punktnummer sowie die Veröffentlichung einer vollständig
fertigen Reihenfolge bleiben erhalten. Bei Kapazität 4.194.304 sinkt die Anzahl
Vergleichsdurchläufe von 253 (ein Schritt) über 132 (zwei Schritte) auf **92 (drei
Schritte)**. Mit Budget 32 lag die neue Reihenfolge der Integration mit 2.276.102
Punkten in allen sechs geprüften Blickrichtungen nach **drei ClientSim-Update-Frames**
vor, gegenüber fünf beim vorherigen Zwei-Schritt-Stand. Alle 139 sichtbaren
Teilmesh-Materialien verwendeten weiterhin die vollständige Reihenfolge.

Unter echtem DX11 wurden sämtliche Ausgabe-Texel mit der Ein-Schritt-Referenz
verglichen: fünf Kapazitäten, vier Richtungen, Gegenrichtung, gleiche Tiefenschlüssel
und Padding. Beide zusammengefassten Varianten stimmten in allen 40 Vergleichen
exakt überein. Sieben vollständige GPU-Rücklesungen der echten Integration fanden
keine ungültigen/doppelten IDs, Tiefeninversionen oder falschen Positionsschlüssel.
Fünf Durchläufe mit wechselnder Variantenreihenfolge ergaben Medianwerte von
11,21 ms (ein Schritt), 6,72 ms (zwei) und 6,20 ms (drei). Diese isolierten Zeiten
enthalten CPU-Aufrufe und synchrones GPU-Rücklesen; sie sind **keine VRChat-FPS-
oder Headset-Latenzmessung**. Details: [VALIDATION.json](VALIDATION.json).

**Bestehende Installation aktualisieren:** Controller, Sortiershader und
Editorvorschau zusammen ersetzen, die mitgelieferten `.meta`-GUIDs erhalten und
UdonSharp im Zielprojekt neu kompilieren. Am Controller `passesPerFrame = 32`
beibehalten. Der schnellere Algorithmus steckt im Quellcode; das Budget einer
älteren Version allein umzustellen aktiviert ihn nicht. Die vorhandenen
Splat-Daten und Zeichenmaterialien müssen für diese Sortieränderung nicht neu
erzeugt werden.

## Historischer VRChat-Test — 9. Oktober 2026

Ich habe den integrierten Renderer am **9. Oktober 2026 im VRChat-Client** getestet. Mit **`passesPerFrame = 32`** war das Nachziehen nach einem Blickwechsel kaum noch wahrnehmbar — visuell geschätzt etwa **0,1 Sekunden**. Die Bildrate erreichte mein **72-FPS-Limit in Virtual Desktop**. Bei der vorherigen Einstellung von 8 Durchläufen waren es ungefähr 0,8 Sekunden und rund 40 FPS.

Der Blick ging dabei durch Fenster; die Anzahl sichtbarer/gezeichneter Splats wurde nicht gemessen. Die 72 FPS sind die erreichte Obergrenze, kein unlimitierter Maximalwert und kein Benchmark für die gesamte sichtbare Stadt. Der Vergleich war nicht kontrolliert: Auflösung, Client-Bedingungen und andere Faktoren waren nicht vereinheitlicht. Deshalb lässt sich die höhere Bildrate nicht eindeutig der Einstellung zuschreiben. Genaue GPU-Framezeit, Client-Version und Headset-/Stereo-Details wurden nicht protokolliert. Ein frischer Import dieser reinen Quellcode-Veröffentlichung muss noch getestet werden.

Diese Beobachtungen betreffen den früheren Quellstand, nicht eine neue Leistungsmessung dieses Updates. Die Editor-Prüfung unter **Unity 2022.3.22f1 / DX11 / NVIDIA GeForce RTX 5070 Ti** verwendete **500.000 Splats**, aufgefüllt auf **524.288**. In drei Blickrichtungen gab es keine fehlenden Einträge, keine Sortierfehler und keine Shader-Diagnosen. Die integrierten Udon-Programme kompilierten. Editor-Messdaten: [VALIDATION.json](VALIDATION.json).

**Die dort aufgeführten 12–51 ms enthalten Sortierung und synchrones GPU-Rücklesen im Editor. Sie sind keine reine GPU-Zeit, keine Kosten pro Frame und kein VRChat-FPS-Benchmark.** Ein belastbares Leistungsbudget für VRChat ist noch nicht bestimmt.

## Warum das Nachziehen verzögert ist

Bei 524.288 Einträgen sind **190 logische Bitonic-Vergleichsschritte** nötig. Der
aktuelle Drei-Schritt-Shader erledigt sie in **70 vollständigen GPU-Vergleichsdurchläufen**,
also drei Update-Frames bei `passesPerFrame = 32`. Das Budget zählt GPU-Durchläufe;
jeder kann bis zu drei logische Schritte ausführen. Initialisierung und
Veröffentlichung sind zusätzliche Durchläufe außerhalb dieses Vergleichsbudgets.

Der Initialwert bleibt 32. Bereits gespeicherte Instanzen behalten ihren eigenen
Wert; mit Budget 8 dauert diese kleinere Sortierung etwa neun Update-Frames.
Vor dem Build den Inspector prüfen. Bei Kapazität 4.194.304 werden aus 253 logischen
Schritten 92 GPU-Vergleichsdurchläufe: drei Update-Frames bei Budget 32. Drei Frames
entsprechen rechnerisch ungefähr 33 ms bei 90 FPS oder 50 ms bei 60 FPS, zusätzlich
zu Scheduling und eventuell bereits laufender Sortierung. Das ist keine gemessene
Gesamtlatenz im Headset.

Der Renderer zeigt erst eine vollständig fertig sortierte Reihenfolge. Bis dahin bleibt die vorige gültige Reihenfolge sichtbar; schnelle Blickwechsel können Transparenzfehler zeigen. Während der Berechnung wird die anfangs erfasste Blickrichtung verwendet. Bei weiterer Bewegung kann danach eine weitere Sortierung nötig werden. Mehr Durchläufe pro Frame verkürzen die Wartezeit, erhöhen aber die Last pro Frame. Die Anzahl nötiger GPU-Durchläufe je Sortierung bleibt für einen gegebenen Shader-Stand gleich. Der Wert ist auf 1–32 begrenzt. Der frühere Client-Vergleich änderte die Instanzeinstellung; dieses Quellcode-Update enthält außerdem die oben beschriebenen Korrekturen.

Auch das Zeichnen verbraucht GPU-Zeit, besonders bei großen, überlappenden transparenten Splats. Anzahl, Auflösung, Überdeckung, GPU, Avatare und die übrige Welt bestimmen die Kosten. Jeder Besucher sortiert lokal; diese Darstellung muss nicht über das Netzwerk synchronisiert werden.

## Installation

Dies ist der **Quellcode-Kern**, kein fertiges Weltpaket und kein vollständiger allgemeiner Importer. Datensätze und erzeugte Assets sind ausgeschlossen. Benötigt werden vier vorbereitete Datentexturen, native Punktmeshes, Zeichenmaterialien und drei Rendertexturen gemäß [DATA_LAYOUT.md](DATA_LAYOUT.md). Ein ursprüngliches `GaussianSplatAsset` kann nicht direkt zugewiesen werden.

1. Ein VRChat-PC-Worlds-Projekt mit SDK-unterstützter Unity-Version, Built-in-Pipeline und UdonSharp verwenden. Das SDK über den offiziellen [Creator Companion](https://vcc.docs.vrchat.com/) installieren.
2. `Runtime`, `Shaders` und `Editor` einschließlich `.meta` nach `Assets/PrempiGaussianDX11` kopieren. Dateinamen und GUIDs erhalten. Keine zweite Kopie von `NeonQuestGaussian` parallel installieren.
3. Eigene, zur Nutzung berechtigte Daten vorbereiten, alle Felder gemäß Einrichtung zuweisen, am Controller **Passes Per Frame = 32** einstellen und UdonSharp im Zielprojekt kompilieren. Erzeugte Udon-Programme gehören zum Zielprojekt.
4. Aktive konfigurierte Clouds werden in Scene-/Game-Ansichten im Edit-Modus automatisch dargestellt. **Tools → Prempi Gaussian DX11 → Preview selected cloud** setzt eine manuell gestoppte Vorschau fort; über das Nachbarmenü stoppen. Jede Kamera besitzt ihre eigene Reihenfolge. Die erste Sortierung läuft synchron, weitere mit dem eingestellten Budget. Während Play und des Übergangs läuft keine Editorvorschau.
5. Mit dem Worlds-SDK im tatsächlichen VRChat-Client auf den Zielsystemen prüfen. Der separate Neuimport dieses Quellpakets ist noch nicht unabhängig getestet.

## Grenzen

- PC/DX11; Android/Quest, DX12, Vulkan, URP und HDRP sind hier nicht validiert. Geometry-Shader werden benötigt.
- Festes RGB, keine Auswertung höherer sphärischer Harmonischer (SH). Daten mit blickabhängigen SH-Farben verlieren diesen Anteil.
- Eine Sortierreihenfolge nach der Kopf-Blickrichtung pro Besucher. Spiegel, Handkamera, parallele Kameras und Stereo-Sonderfälle sind nicht gesondert validiert.
- Statische Daten und Cloud-Transformation angenommen. Eine Cloud-Drehung/Skalierung bei unverändertem Blick löst keine eigene Neusortierung aus.
- Andere transparente Objekte werden nicht automatisch korrekt mit den Splats verschachtelt; der Shader schreibt keine Tiefe.
- Echte Cloud-Bounds, Punkt-Topologie und aufsteigende Material-Queues sind erforderlich.

## Umfang und Lizenz

Controller, zwei Shader, Editorvorschau und Speicherschutz, Unity-Textmetadaten, Dokumentation und SHA-256-Dateiliste. Keine Beispieldaten, Weltdateien, privaten Kontaktdaten, Zugangsdaten, Projektkonfigurationen, Logs, Bilder, Binärdateien oder SDK-Inhalte. Die Renderer-Quellen entsprechen der aktuellen integrierten Implementierung; bestehende öffentliche Unity-GUIDs bleiben erhalten.

**MIT**, ursprüngliches Copyright von Aras erhalten, Anpassungen von Prempi, entwickelt mit ChatGPT/Codex-Unterstützung. Andere VRChat-Lösungen existieren bereits, etwa [MichaelMoroz/VRChatGaussianSplatting](https://github.com/MichaelMoroz/VRChatGaussianSplatting). Ein allgemeiner Qualitäts- oder Leistungsvergleich wurde nicht nachgewiesen. Unabhängige Veröffentlichung ohne offizielle VRChat-Zusage oder Behauptung einer weltweit ersten DX11-Implementierung. Rechte an importierten Datensätzen sind gesondert zu prüfen. Details: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
