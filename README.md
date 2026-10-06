# DungeonGen
Dungeon generator in unity

## Demo web (WebGL)

La escena `Assets/DungeonGenerator/Dungeon2D.unity` está preparada como demo:

- **Generar / Aleatoria (R)**: regenera la mazmorra con la semilla indicada o una aleatoria.
- **Vista general**: arrastrar para orbitar, WASD o botón central para desplazar, rueda o pellizco para zoom.
- **Explorar (Tab)**: primera persona desde la sala de inicio. WASD/flechas para andar, Shift para correr, arrastrar para mirar. Se camina sobre el NavMesh generado, que hace de colisión.
- **Ver algoritmo paso a paso (V)**: muestra cómo se construye la mazmorra actual, con vista cenital y animación en cada paso:
  1. Salas aleatorias (la morada es la de inicio).
  2. Triangulación de Delaunay entre los centros de las salas.
  3. Árbol de expansión mínima (Prim), en verde.
  4. Ciclos extra: un 12,5 % de las aristas descartadas, en azul.
  5. Pasillos trazados con A*, en naranja, y las puertas en verde.

  Espacio / Retroceso para avanzar y volver. Al pasar del último paso se vuelve a la mazmorra.
- **H**: mostrar/ocultar la ayuda.
- **Copiar enlace**: copia la URL de la página con `?seed=<semilla>`. Al abrir ese enlace se genera la misma mazmorra. La barra de direcciones también se actualiza con la semilla actual.

> En itch.io el juego corre dentro de un iframe, así que los parámetros de la página de itch no llegan al juego. El enlace copiado apunta a la página del propio build (html.itch.zone), que sí funciona. En hostings propios (GitHub Pages, Netlify...) funciona directamente.

### Compilar para web

1. Instalar el módulo **WebGL Build Support** para Unity 2022.3.46f1 desde Unity Hub.
2. `File > Build Settings`, seleccionar **WebGL** y pulsar **Switch Platform** (la escena `Dungeon2D` ya está en la lista).
3. **Build** en una carpeta, por ejemplo `Build/WebGL`.
4. Subir el contenido de la carpeta a [itch.io](https://itch.io) (como ZIP, marcando "This file will be played in the browser") o a cualquier hosting estático.

La compresión es Gzip con *Decompression Fallback* activado, así que funciona también en hostings que no envían la cabecera `Content-Encoding` (por ejemplo GitHub Pages). Para probar en local hay que servirlo por HTTP (`python -m http.server` en la carpeta del build), no abriendo el `index.html` directamente.
