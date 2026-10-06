mergeInto(LibraryManager.library, {

  // Cambia la URL de la barra de direcciones sin recargar la página
  DungeonDemo_ReplaceUrl: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    try {
      window.history.replaceState(null, "", url);
    } catch (e) {
      console.warn("DungeonDemo: no se pudo actualizar la URL", e);
    }
  },

  DungeonDemo_CopyToClipboard: function (textPtr) {
    var text = UTF8ToString(textPtr);

    function fallback() {
      var textArea = document.createElement("textarea");
      textArea.value = text;
      textArea.style.position = "fixed";
      textArea.style.opacity = "0";
      document.body.appendChild(textArea);
      textArea.select();
      try {
        document.execCommand("copy");
      } catch (e) {
        console.warn("DungeonDemo: no se pudo copiar al portapapeles", e);
      }
      document.body.removeChild(textArea);
    }

    if (navigator.clipboard && window.isSecureContext) {
      navigator.clipboard.writeText(text).catch(fallback);
    } else {
      fallback();
    }
  }
});
