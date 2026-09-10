// Browser-side file download for the BladeLoop run report.
//
// Unity WebGL has no disk. File.WriteAllText "succeeds" against an IndexedDB
// virtual filesystem the user can never browse to, so the Windows save path
// silently produces an unreachable file. The only real way to hand a file to
// someone in a browser is a Blob plus a synthetic anchor click, which is what
// this does.
//
// Lives under Assets/Plugins/WebGL/ so Unity sets the platform on it
// automatically; a .jslib anywhere else is ignored unless configured by hand.
mergeInto(LibraryManager.library, {

  BladeLoopDownloadFile: function (namePtr, textPtr) {
    try {
      var name = UTF8ToString(namePtr);
      var text = UTF8ToString(textPtr);

      var blob = new Blob([text], { type: 'text/html;charset=utf-8' });
      var url  = URL.createObjectURL(blob);

      var a = document.createElement('a');
      a.href = url;
      a.download = name;
      a.style.display = 'none';

      // Must be in the document for the click to count as a user gesture in
      // Firefox; Chrome tolerates a detached node but there is no reason to
      // rely on that.
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);

      // Revoking immediately cancels the download in Safari, so give it a beat.
      setTimeout(function () { URL.revokeObjectURL(url); }, 2000);
    } catch (e) {
      console.error('[BladeLoop] report download failed:', e);
    }
  }

});
