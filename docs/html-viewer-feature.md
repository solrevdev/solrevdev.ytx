# HTML Viewer Feature Implementation

## Overview
Implement a self-contained HTML viewer that allows users to paste JSON output from `ytx` and view it in a formatted interface with copy/download capabilities. This will be implemented as Option 3 (Hybrid Approach) - included in the main repository but also deployable to GitHub Pages.

## Feature Requirements
- Single-file HTML page with no external dependencies
- Paste JSON input from `ytx` tool output
- Display formatted video metadata (title, URL, description)
- Show both raw and markdown transcript with copy buttons
- Download capabilities for markdown and JSON files
- Responsive design for desktop and mobile
- Dark theme matching modern development tools
- Offline functionality

## Implementation Tasks

### Task 1: Create HTML Viewer File
Create `web/viewer.html` with the following complete source code:

```html
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>YouTube JSON → Viewer</title>
<style>
  :root { --bg:#0b0f14; --card:#111826; --muted:#8892a6; --text:#e6edf3; --accent:#6ea8fe; }
  html,body{margin:0;padding:0;background:var(--bg);color:var(--text);font:14px/1.45 system-ui,-apple-system,Segoe UI,Roboto,Ubuntu,Cantarell,"Noto Sans",Arial;}
  header{padding:18px 16px;border-bottom:1px solid #1d2636;background:linear-gradient(180deg,#0b0f14,#0a111c);}
  h1{margin:0;font-size:18px;letter-spacing:.2px}
  main{max-width:1100px;margin:0 auto;padding:20px 12px 80px}
  .row{display:grid;gap:16px;grid-template-columns:1fr;align-items:start}
  @media(min-width:1000px){.row{grid-template-columns:420px 1fr}}
  .card{background:var(--card);border:1px solid #1d2636;border-radius:12px;padding:14px}
  .card h2{margin:0 0 10px 0;font-size:15px;color:#cbd5e1}
  textarea, input, button {font:inherit}
  textarea, input[type="file"]{width:100%;box-sizing:border-box;background:#0b121f;color:var(--text);border:1px solid #1d2636;border-radius:8px;padding:10px;outline:none}
  textarea:focus{border-color:#2b3a55}
  .btn{display:inline-flex;gap:8px;align-items:center;padding:8px 10px;border:1px solid #27324a;background:#0f172a;color:var(--text);border-radius:8px;cursor:pointer}
  .btn:hover{border-color:#334062}
  .stack{display:flex;gap:8px;flex-wrap:wrap}
  .muted{color:var(--muted)}
  .kv{display:grid;grid-template-columns:140px 1fr;gap:8px;align-items:start}
  .kv div:first-child{color:#9fb0cb}
  pre{white-space:pre-wrap;word-wrap:break-word;background:#0b121f;border:1px solid #1d2636;border-radius:8px;padding:12px;margin:0}
  .copy{float:right}
  .pill{display:inline-block;border:1px solid #2d3a56;padding:2px 8px;border-radius:999px;color:#c7d2fe;background:#0e1530}
  .footer{margin-top:16px;color:#8da2bf}
  .link a{color:var(--accent);text-decoration:none}
  .link a:hover{text-decoration:underline}
</style>
</head>
<body>
<header><h1>YouTube JSON → Viewer</h1></header>
<main>
  <div class="row">
    <section class="card">
      <h2>1) Paste JSON</h2>
      <textarea id="jsonIn" rows="14" placeholder='Paste the JSON from ytx or the shell one-liner here...'></textarea>
      <div class="stack" style="margin-top:10px">
        <button class="btn" id="loadBtn">Load</button>
        <label class="btn" for="file">Load .json <input id="file" type="file" accept="application/json" style="display:none"></label>
        <button class="btn" id="sampleBtn">Sample</button>
        <button class="btn" id="clearBtn">Clear</button>
      </div>
      <p class="footer muted">Expected shape:
        <span class="pill">url</span> <span class="pill">title</span> <span class="pill">description</span>
        <span class="pill">transcriptRaw</span> <span class="pill">transcript</span>
      </p>
    </section>

    <section class="card">
      <h2>2) Summary</h2>
      <div class="kv">
        <div>Title</div><div id="title" class="link">—</div>
        <div>URL</div><div id="url" class="link">—</div>
        <div>Description</div><div id="desc">—</div>
      </div>
    </section>

    <section class="card">
      <h2>Transcript (Markdown)</h2>
      <div class="stack" style="margin-bottom:8px">
        <button class="btn copy" data-target="mdOut">Copy</button>
        <button class="btn" id="downloadMd">Download .md</button>
      </div>
      <pre id="mdOut">—</pre>
    </section>

    <section class="card">
      <h2>Transcript (Raw)</h2>
      <div class="stack" style="margin-bottom:8px">
        <button class="btn copy" data-target="rawOut">Copy</button>
        <button class="btn" id="downloadJson">Download merged .json</button>
      </div>
      <pre id="rawOut">—</pre>
    </section>
  </div>
</main>

<script>
(function(){
  const $ = s => document.querySelector(s);
  const jsonIn = $("#jsonIn");
  const set = (id, html) => { const el = $("#"+id); el.innerHTML = html || "—"; };

  function loadObj(obj){
    try{
      set("title", obj.title ? `<a target="_blank" rel="noopener" href="${obj.url||'#'}">${escapeHtml(obj.title)}</a>` : "—");
      set("url", obj.url ? `<a target="_blank" rel="noopener" href="${obj.url}">${obj.url}</a>` : "—");
      set("desc", obj.description ? `<pre>${escapeHtml(obj.description)}</pre>` : "—");
      set("mdOut", obj.transcript ? escapeHtml(obj.transcript) : "—");
      set("rawOut", obj.transcriptRaw ? escapeHtml(obj.transcriptRaw) : "—");
      window.__lastObj = obj;
    }catch(e){ alert("Render error: " + e.message); }
  }

  function parseAndLoad(){
    try{
      const obj = JSON.parse(jsonIn.value);
      loadObj(obj);
    }catch(e){ alert("Invalid JSON: " + e.message); }
  }

  function escapeHtml(s){
    return String(s)
      .replaceAll("&","&amp;").replaceAll("<","&lt;").replaceAll(">","&gt;")
      .replaceAll('"',"&quot;").replaceAll("'","&#39;");
  }

  function download(name, mime, text){
    const blob = new Blob([text], {type: mime});
    const a = document.createElement("a");
    a.href = URL.createObjectURL(blob);
    a.download = name;
    document.body.appendChild(a); a.click(); a.remove();
    setTimeout(()=>URL.revokeObjectURL(a.href), 1500);
  }

  document.addEventListener("click", e=>{
    const t = e.target;
    if(t.classList.contains("copy")){
      const target = t.getAttribute("data-target");
      const data = $("#"+target).innerText;
      navigator.clipboard.writeText(data).then(()=>{ t.textContent="Copied!"; setTimeout(()=>t.textContent="Copy",1200); });
    }
  });

  $("#loadBtn").addEventListener("click", parseAndLoad);
  $("#clearBtn").addEventListener("click", ()=>{ jsonIn.value=""; set("title"); set("url"); set("desc"); set("mdOut"); set("rawOut"); window.__lastObj=null; });

  $("#file").addEventListener("change", async (e)=>{
    const f = e.target.files[0]; if(!f) return;
    const text = await f.text();
    jsonIn.value = text;
    parseAndLoad();
  });

  $("#downloadMd").addEventListener("click", ()=>{
    const obj = window.__lastObj; if(!obj) return alert("Load JSON first.");
    const base = (obj.title||"transcript").replace(/[^\w\-]+/g,"-").replace(/-+/g,"-");
    download(base + ".md", "text/markdown;charset=utf-8", obj.transcript || "");
  });

  $("#downloadJson").addEventListener("click", ()=>{
    const obj = window.__lastObj; if(!obj) return alert("Load JSON first.");
    const base = (obj.title||"video").replace(/[^\w\-]+/g,"-").replace(/-+/g,"-");
    download(base + ".json", "application/json;charset=utf-8", JSON.stringify(obj,null,2));
  });

  $("#sampleBtn").addEventListener("click", ()=>{
    const sample = {
      "url":"https://www.youtube.com/watch?v=dQw4w9WgXcQ",
      "title":"Sample Title",
      "description":"Sample description.",
      "transcriptRaw":"This is a raw transcript example…",
      "transcript":"- [00:03](https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=3s) Intro line\n- [00:12](https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=12s) Second line"
    };
    jsonIn.value = JSON.stringify(sample, null, 2);
    parseAndLoad();
  });
})();
</script>
</body>
</html>
```

### Task 2: Update Main README.md
Add a new section to the main `README.md` file after the installation section:

```markdown
## HTML Viewer

For a visual way to view and work with the JSON output, use the included HTML viewer:

- **Local**: Open `web/viewer.html` in any browser
- **Online**: Visit [https://solrevdev.github.io/solrevdev.ytx/](https://solrevdev.github.io/solrevdev.ytx/)

The viewer allows you to:
- Paste JSON output and view formatted content
- Copy transcript text to clipboard  
- Download transcript as markdown (.md)
- Download complete data as JSON

### Usage Example
```bash
# Generate JSON and view in HTML
ytx "https://www.youtube.com/watch?v=dQw4w9WgXcQ" > output.json
# Then paste the JSON content into the viewer
```
```

### Task 3: Enable GitHub Pages
1. Go to repository Settings → Pages
2. Set Source to "Deploy from a branch"
3. Select branch: `master`
4. Select folder: `/ (root)`
5. Save settings

### Task 4: Create GitHub Pages Configuration
Create `_config.yml` in the root directory:

```yaml
# GitHub Pages configuration for solrevdev.ytx
title: "ytx - YouTube Transcript Extractor"
description: "A .NET Global Tool that extracts YouTube video metadata and transcripts as structured JSON"
baseurl: "/solrevdev.ytx"
url: "https://solrevdev.github.io"

# Redirect root to viewer
plugins:
  - jekyll-redirect-from

# Include web directory in build
include:
  - web/

# Set default layout
defaults:
  - scope:
      path: ""
    values:
      layout: default
```

### Task 5: Create Index Page for GitHub Pages
Create `index.html` in the root directory that redirects to the viewer:

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>ytx - YouTube Transcript Extractor</title>
    <meta http-equiv="refresh" content="0; url=./web/viewer.html">
    <style>
        body { 
            font-family: system-ui, -apple-system, sans-serif; 
            text-align: center; 
            padding: 50px; 
            background: #0b0f14; 
            color: #e6edf3; 
        }
        a { color: #6ea8fe; text-decoration: none; }
        a:hover { text-decoration: underline; }
    </style>
</head>
<body>
    <h1>ytx - YouTube Transcript Extractor</h1>
    <p>Redirecting to HTML viewer...</p>
    <p>If you are not redirected automatically, <a href="./web/viewer.html">click here</a>.</p>
    
    <hr style="margin: 40px 0; border: 1px solid #1d2636;">
    
    <h2>About ytx</h2>
    <p>A .NET Global Tool that extracts YouTube video metadata and transcripts as structured JSON.</p>
    
    <h3>Installation</h3>
    <pre style="background: #111826; padding: 15px; border-radius: 8px; text-align: left; max-width: 500px; margin: 20px auto;">dotnet tool install -g solrevdev.ytx</pre>
    
    <h3>Usage</h3>
    <pre style="background: #111826; padding: 15px; border-radius: 8px; text-align: left; max-width: 500px; margin: 20px auto;">ytx "https://www.youtube.com/watch?v=VIDEO_ID"</pre>
    
    <p><a href="https://github.com/solrevdev/solrevdev.ytx">View on GitHub</a> | 
       <a href="https://www.nuget.org/packages/solrevdev.ytx">NuGet Package</a></p>
</body>
</html>
```

### Task 6: Update .gitignore
Ensure the following entries are in `.gitignore` to avoid committing unwanted files:

```gitignore
# GitHub Pages
_site/
.sass-cache/
.jekyll-cache/
.jekyll-metadata

# Don't ignore web directory (we want to include it)
!web/
```

### Task 7: Testing Instructions
1. **Local Testing**: Open `web/viewer.html` directly in browser
2. **Test with Sample Data**: Click "Sample" button to load example JSON
3. **Test Real Data**: Run `ytx` command and paste output
4. **Test Downloads**: Verify markdown and JSON downloads work
5. **Test Copy Functions**: Verify clipboard copy buttons work
6. **Mobile Testing**: Test responsive design on mobile devices

### Task 8: GitHub Pages Deployment Testing
1. Push changes to master branch
2. Wait for GitHub Pages build (check Actions tab)
3. Visit `https://solrevdev.github.io/solrevdev.ytx/`
4. Verify redirect to viewer works
5. Test all functionality in hosted environment

## Technical Details

### Browser Compatibility
- Modern browsers with ES6+ support
- Clipboard API for copy functionality
- File API for JSON file uploads
- Blob API for downloads

### Security Considerations
- No external dependencies (fully self-contained)
- HTML escaping for user input
- Safe JSON parsing with error handling
- No server-side processing required

### File Structure After Implementation
```
solrevdev.ytx/
├── web/
│   └── viewer.html          # HTML viewer component
├── index.html               # GitHub Pages entry point
├── _config.yml             # GitHub Pages configuration  
├── README.md               # Updated with viewer documentation
└── ... (existing files)
```

## Success Criteria
- [ ] HTML viewer file created and functional locally
- [ ] GitHub Pages successfully serving the viewer
- [ ] Main README.md updated with viewer documentation
- [ ] All viewer features working (paste, load, copy, download)
- [ ] Responsive design working on mobile and desktop
- [ ] No external dependencies or network requests required
- [ ] Clean integration with existing project structure

## Future Enhancements (Optional)
- Syntax highlighting for JSON input
- Dark/light theme toggle
- Export to other formats (CSV, TXT)
- Batch processing multiple JSON files
- URL parameter support for direct JSON loading
- PWA capabilities for offline use