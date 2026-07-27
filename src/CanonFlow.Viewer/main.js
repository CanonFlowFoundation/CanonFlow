import './style.css'

const dropZone = document.getElementById('drop-zone');
const fileInput = document.getElementById('file-input');
const dashboard = document.getElementById('dashboard');
const btnReset = document.getElementById('btn-reset');

// UI Elements
const uiOverallVerdict = document.getElementById('overall-verdict');
const uiVerdictBanner = document.getElementById('verdict-banner');
const uiHashContext = document.getElementById('hash-context');
const uiHashSubject = document.getElementById('hash-subject');
const uiHashSeal = document.getElementById('hash-seal');
const uiMetaInfo = document.getElementById('meta-info');
const uiAssessmentsList = document.getElementById('assessments-list');

// Event Listeners for Drag & Drop
dropZone.addEventListener('click', () => fileInput.click());

dropZone.addEventListener('dragover', (e) => {
  e.preventDefault();
  dropZone.classList.add('dragover');
});

dropZone.addEventListener('dragleave', () => {
  dropZone.classList.remove('dragover');
});

dropZone.addEventListener('drop', (e) => {
  e.preventDefault();
  dropZone.classList.remove('dragover');
  
  if (e.dataTransfer.files.length > 0) {
    handleFile(e.dataTransfer.files[0]);
  }
});

fileInput.addEventListener('change', (e) => {
  if (e.target.files.length > 0) {
    handleFile(e.target.files[0]);
  }
});

btnReset.addEventListener('click', () => {
  dashboard.style.display = 'none';
  dropZone.style.display = 'flex';
  fileInput.value = '';
});

function handleFile(file) {
  if (!file.name.endsWith('.json')) {
    alert('Please upload a valid JSON file.');
    return;
  }

  const reader = new FileReader();
  reader.onload = (e) => {
    try {
      const data = JSON.parse(e.target.result);
      renderDashboard(data);
    } catch (err) {
      alert('Failed to parse JSON file: ' + err.message);
    }
  };
  reader.readAsText(file);
}

function renderDashboard(data) {
  // Hide dropzone, show dashboard
  dropZone.style.display = 'none';
  dashboard.style.display = 'block';

  // Extract Verdict
  const verdict = data.Verdict || 'Unknown';
  uiOverallVerdict.textContent = verdict;
  uiVerdictBanner.className = 'verdict-banner ' + verdict;

  // Hashes and Seal
  uiHashContext.textContent = data.Context?.Hash || 'N/A';
  uiHashSubject.textContent = data.Subject?.Hash || 'N/A';
  
  if (data.Seal && data.Seal.Signature) {
    uiHashSeal.textContent = data.Seal.Signature;
  } else {
    uiHashSeal.textContent = 'Unsigned';
  }

  // Meta info
  const evaluatorId = data.Evaluator?.Id || 'Unknown Evaluator';
  const schemaVer = data.SchemaVersion || '1.0';
  const timestamp = data.Context?.Instant || new Date().toISOString();
  
  uiMetaInfo.innerHTML = `
    <span>Evaluator:</span> ${evaluatorId} &nbsp;&nbsp;|&nbsp;&nbsp;
    <span>Schema:</span> ${schemaVer} &nbsp;&nbsp;|&nbsp;&nbsp;
    <span>Date:</span> ${new Date(timestamp).toLocaleString()}
  `;

  // Assessments
  uiAssessmentsList.innerHTML = '';
  
  if (data.Assessments && Array.isArray(data.Assessments)) {
    data.Assessments.forEach((assessment, i) => {
      const compId = assessment.ComponentId || `Component ${i+1}`;
      const compVer = assessment.ComponentVersion || 'v?';
      const health = assessment.Health || 'Unknown';
      const compliance = assessment.Compliance || 'Unknown';
      
      const el = document.createElement('div');
      el.className = 'assessment-item fade-in-up';
      el.style.animationDelay = `${0.3 + (i * 0.1)}s`;
      
      el.innerHTML = `
        <div class="badge ${health}">${health}</div>
        <div class="assessment-details">
          <h4>${compId} <span style="color:var(--text-secondary);font-size:0.9rem;font-weight:normal">@ ${compVer}</span></h4>
          <p>Compliance Verdict: <strong style="color:var(--text-primary)">${compliance}</strong></p>
        </div>
        <div class="assessment-stats" style="text-align:right">
          <div style="font-size:0.8rem;color:var(--text-secondary)">Rules Assessed</div>
          <div style="font-weight:600">${assessment.EvaluatedRules || 0} / ${assessment.ApplicableRules || 0}</div>
        </div>
      `;
      
      uiAssessmentsList.appendChild(el);
    });
  } else {
    uiAssessmentsList.innerHTML = '<p style="color:var(--text-secondary)">No component assessments found.</p>';
  }
}
