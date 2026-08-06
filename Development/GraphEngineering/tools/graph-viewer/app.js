(() => {
  'use strict';

  const SVG_NS = 'http://www.w3.org/2000/svg';
  const STATIC = window.__MYUNITYMCP_STATIC_SNAPSHOTS__ || null;
  const LABELS = {
    eligible:'実行可能', running:'実行中', pending:'未着手',
    complete:'完了', approval:'Human Gate',
    awaiting_approval:'承認待ち', blocked:'Blocked'
  };
  const PHASE_NAMES = {
    phase_00_release_integrity:'Release Integrity',
    phase_01_unity_agent_runtime:'UnityAgentMCP Runtime',
    phase_02_world_creator:'WorldCreator Runtime',
    phase_03_profiler_mcp:'ProfilerMCP',
    phase_04_build_mcp:'BuildMCP',
    phase_05_addressables_mcp:'AddressablesMCP',
    phase_06_ui_mcp:'UIMCP',
    phase_07_animation_mcp:'AnimationMCP',
    phase_08_audio_mcp:'AudioMCP',
    phase_09_cinematic_mcp:'CinematicMCP',
    phase_10_movie_creator:'MovieCreator',
    phase_11_live_creator:'LiveCreator',
    phase_12_production_hardening:'Production Hardening'
  };
  const DESCRIPTIONS = {
    bootstrap_development_harness:'Codex開発Harness、Graph、State、Evidence検証をRepositoryへ導入します。',
    phase_00_release_integrity:'Release identity、Version、Tag、Main、Evidenceの整合性を確立します。',
    phase_01_unity_agent_runtime:'Domain MCPを統括する製品Control Planeを実装します。',
    phase_02_world_creator:'Visual GoalをGraphicsMCPの実行Planへ変換する最初のCreator Runtimeです。',
    phase_03_profiler_mcp:'Editor計測、Baseline比較、環境Evidenceを提供します。',
    phase_04_build_mcp:'Build設定とBuild実行を別Approvalで扱います。',
    phase_05_addressables_mcp:'Addressablesの検査、Typed Mutation、Content Buildを扱います。',
    phase_06_ui_mcp:'uGUIまたはUI ToolkitをTyped Planで検査・変更します。',
    phase_07_animation_mcp:'Animator State Graphを検査・変更・Undoします。',
    phase_08_audio_mcp:'AudioSourceと既存Mixer routingのPublic API範囲を実装します。',
    phase_09_cinematic_mcp:'Timeline、Cinemachine、Animation、AudioのBindingを統合します。',
    phase_10_movie_creator:'Narrative GoalからShot SequenceとContinuity Reviewまで実行します。',
    phase_11_live_creator:'CueやTimecodeからCharacter-firstなLive Sequenceを統合します。',
    phase_12_production_hardening:'互換性、Security、Failure Recovery、External Client E2Eを完成させます。',
    project_completion_gate:'全Phase、Evidence、Safety、Docs整合性を機械判定します。',
    human_final_release_approval:'人間が最終Release Candidateを確認して承認します。',
    project_complete:'Phase 0〜12と最終承認を満たした唯一のTerminal Nodeです。'
  };
  const GATES = {
    bootstrap_development_harness:'Harness validation、State validation、Completion Gate negative testがすべて成功すること。',
    phase_00_release_integrity:'Release Policyと現在のRelease状態が一致し、CIが再発を拒否すること。',
    phase_01_unity_agent_runtime:'GraphicsMCPへの安全な委譲がE2Eで成功すること。',
    phase_02_world_creator:'Creatorが直接Mutationせず、Human Reviewまで到達すること。',
    phase_03_profiler_mcp:'Editor値を実機性能として誤表現せず、再現可能なCaptureが取れること。',
    phase_04_build_mcp:'BuildReport、Artifact Hash、Secret Redactionが検証されること。',
    phase_05_addressables_mcp:'Package GateとContent Build Evidenceが揃うこと。',
    phase_06_ui_mcp:'最低1つのBackendでE2Eが成功し、Visual Reviewへ渡せること。',
    phase_07_animation_mcp:'Typed MutationとRevision安全性がTestされること。',
    phase_08_audio_mcp:'非対応Mixer Topologyを成功扱いせず正しく拒否すること。',
    phase_09_cinematic_mcp:'Package Version GateとShot Sequence E2Eが成功すること。',
    phase_10_movie_creator:'Sample MovieがE2EでHuman Visual Reviewへ到達すること。',
    phase_11_live_creator:'TimingとCharacter Visibilityが検証されること。',
    phase_12_production_hardening:'Release CandidateとProduction Evidenceが揃うこと。',
    project_completion_gate:'全必須NodeとEvidenceが完了し、Critical Safety Issueがないこと。',
    human_final_release_approval:'明示的なHuman Final Release Approvalが記録されること。',
    project_complete:'Human Final Release Approvalからのみ遷移できます。'
  };
  const RUNTIME_EVIDENCE = {
    normalize_intent:['normalized_goal'], inspect_capabilities:['capability_snapshot'],
    select_domains:['selected_domains'], activate_tool_groups:['activated_tool_groups'],
    inspect:['domain_findings'], compile_integrated_plan:['integrated_plan'],
    human_approval:['approval_token'], apply_domain_plans:['apply_results'],
    save_bake_build:['side_effect_results'], collect_evidence:['execution_evidence'],
    machine_evaluation:['machine_evaluation'], human_review:['human_review'],
    refine:['refinement_decision'], execution_complete:['accepted_execution']
  };
  const LAYOUT_IMPLEMENTATION = {
    bootstrap_development_harness:[300,40], phase_00_release_integrity:[300,145],
    phase_01_unity_agent_runtime:[300,250], phase_02_world_creator:[300,355],
    phase_03_profiler_mcp:[40,500], phase_04_build_mcp:[300,500], phase_05_addressables_mcp:[560,500],
    phase_06_ui_mcp:[300,645], phase_07_animation_mcp:[300,750], phase_08_audio_mcp:[300,855],
    phase_09_cinematic_mcp:[300,960], phase_10_movie_creator:[300,1065], phase_11_live_creator:[300,1170],
    phase_12_production_hardening:[300,1275], project_completion_gate:[300,1380],
    human_final_release_approval:[300,1485], project_complete:[300,1590]
  };

  const els = Object.fromEntries([
    'implementationTab','runtimeTab','searchInput','statusFilter','graphSvg',
    'totalMetric','completeMetric','eligibleMetric','terminalMetric',
    'detailTitle','detailStatus','detailDescription','detailDependencies',
    'detailEvidence','detailGate','projectStatus','currentNode',
    'attemptCount','lastUpdated','liveRegion'
  ].map(id => [id, document.getElementById(id)]));

  const state = {mode:'implementation', selected:null, query:'', filter:'all', snapshots:{}, refreshTimer:null};

  function svg(name, attrs={}) {
    const element = document.createElementNS(SVG_NS, name);
    Object.entries(attrs).forEach(([key,value]) => element.setAttribute(key,String(value)));
    return element;
  }
  function snapshot() { return state.snapshots[state.mode]; }
  function runtimeLayout(graph) {
    const result = {};
    Object.keys(graph.nodes).forEach((id,index) => result[id] = [300,40 + index * 105]);
    return result;
  }
  function layout() { return state.mode === 'implementation' ? LAYOUT_IMPLEMENTATION : runtimeLayout(snapshot().graph); }
  function meta(id,node) {
    if (state.mode === 'implementation') {
      if (id === 'bootstrap_development_harness') return {title:'Harness Bootstrap',subtitle:'開発Harness構築'};
      if (id === 'project_completion_gate') return {title:'Completion Gate',subtitle:'全Phase統合検証'};
      if (id === 'human_final_release_approval') return {title:'Human Approval',subtitle:'最終Release承認'};
      if (id === 'project_complete') return {title:'Project Complete',subtitle:'Terminal Goal達成'};
      const match = id.match(/^phase_(\d+)_/);
      return {title:match ? `Phase ${Number(match[1])}` : id, subtitle:PHASE_NAMES[id] || node.type || ''};
    }
    return {title:node.label || id.replaceAll('_',' '), subtitle:node.type || ''};
  }
  function description(id,node) { return DESCRIPTIONS[id] || node.description || node.spec || 'Node固有の説明は未登録です。'; }
  function gate(id,node) {
    if (GATES[id]) return GATES[id];
    if (id === 'normalize_intent') return 'Goal、Scope、禁止変更、Acceptanceが明示されること。';
    if (id === 'human_approval') return 'ScopeとRevisionに紐づいた有効なApproval Tokenがあること。';
    if (id === 'human_review') return '自動でBeauty Passを宣言しないこと。';
    if (id === 'execution_complete') return 'Machine Acceptanceと必要なHuman Reviewが成立すること。';
    return 'Node固有のAcceptanceとRequired Evidenceを満たすこと。';
  }
  function nodeState(id,node) {
    if (state.mode === 'implementation') {
      const raw = snapshot().state?.nodes?.[id] || {status:'pending',evidence:{},attempts:[]};
      let status = raw.status || 'pending';
      if (status === 'pending') {
        const deps = node.depends_on || [];
        if (deps.every(dep => snapshot().state?.nodes?.[dep]?.status === 'complete')) status = 'eligible';
      }
      if (node.type === 'human_gate' && !['complete','running','blocked'].includes(status)) status = 'approval';
      return {...raw,status};
    }
    return {status: node.type === 'human_gate' ? 'approval' : id === Object.keys(snapshot().graph.nodes)[0] ? 'eligible' : 'pending', evidence:{}, attempts:[]};
  }
  function statusColor(status) {
    return {eligible:'var(--viz-accent)', running:'var(--viz-series-2)', complete:'var(--viz-series-1)', approval:'var(--viz-series-3)', awaiting_approval:'var(--viz-series-3)', blocked:'var(--viz-series-4)'}[status] || 'var(--viz-card)';
  }
  function visible(id,node) {
    const info = meta(id,node);
    const text = `${id} ${info.title} ${info.subtitle} ${description(id,node)}`.toLowerCase();
    const status = nodeState(id,node).status;
    return (!state.query || text.includes(state.query.toLowerCase())) && (state.filter === 'all' || status === state.filter);
  }
  function visualDependencies(id,node) {
    if (id === 'project_completion_gate') return ['phase_12_production_hardening'];
    return node.depends_on || [];
  }
  function edgePath(source,target) {
    const w=260,h=82;
    if (Math.abs(source[1]-target[1]) < 8) {
      const x1=source[0]+w,y1=source[1]+h/2,x2=target[0],y2=target[1]+h/2;
      const mid=(x1+x2)/2;
      return `M ${x1} ${y1} C ${mid} ${y1}, ${mid} ${y2}, ${x2} ${y2}`;
    }
    const x1=source[0]+w/2,y1=source[1]+h,x2=target[0]+w/2,y2=target[1];
    const mid=(y1+y2)/2;
    return `M ${x1} ${y1} C ${x1} ${mid}, ${x2} ${mid}, ${x2} ${y2}`;
  }
  function renderGraph() {
    const snap=snapshot(), nodes=snap.graph.nodes, positions=layout();
    const height=state.mode==='implementation' ? 1700 : Math.max(620,Object.keys(nodes).length*105+70);
    els.graphSvg.setAttribute('viewBox',`0 0 920 ${height}`);
    els.graphSvg.replaceChildren();
    const defs=svg('defs');
    const marker=svg('marker',{id:'arrow',viewBox:'0 0 10 10',refX:9,refY:5,markerWidth:7,markerHeight:7,orient:'auto-start-reverse'});
    marker.appendChild(svg('path',{d:'M 0 0 L 10 5 L 0 10 z',fill:'var(--viz-border)'}));
    defs.appendChild(marker); els.graphSvg.appendChild(defs);

    Object.entries(nodes).forEach(([id,node]) => {
      const target=positions[id];
      visualDependencies(id,node).forEach(dep => {
        const source=positions[dep]; if (!source || !target) return;
        const path=svg('path',{d:edgePath(source,target),class:'edge','marker-end':'url(#arrow)'});
        const targetStatus=nodeState(id,node).status;
        const sourceStatus=nodeState(dep,nodes[dep]||{}).status;
        if (targetStatus==='eligible'||targetStatus==='running'||sourceStatus==='complete') path.classList.add('is-active');
        els.graphSvg.appendChild(path);
      });
    });

    Object.entries(nodes).forEach(([id,node]) => {
      const pos=positions[id]; if (!pos) return;
      const info=meta(id,node), ns=nodeState(id,node);
      const group=svg('g',{class:`node-group${state.selected===id?' is-selected':''}${visible(id,node)?'':' is-dimmed'}`,transform:`translate(${pos[0]} ${pos[1]})`,role:'button','aria-label':`${info.title} ${info.subtitle} ${LABELS[ns.status]||ns.status}`});
      group.appendChild(svg('rect',{class:'node-box',width:260,height:82}));
      group.appendChild(svg('circle',{class:'status-dot',cx:25,cy:25,r:9,fill:statusColor(ns.status)}));
      const title=svg('text',{x:45,y:30,'font-size':18,'font-weight':650}); title.textContent=info.title; group.appendChild(title);
      const subtitle=svg('text',{class:'node-subtitle',x:45,y:56,'font-size':15}); subtitle.textContent=info.subtitle; group.appendChild(subtitle);
      const status=svg('text',{class:'node-state-label',x:235,y:70,'text-anchor':'end','font-size':11}); status.textContent=LABELS[ns.status]||ns.status; group.appendChild(status);
      group.addEventListener('click',()=>selectNode(id)); els.graphSvg.appendChild(group);
    });
  }
  function renderMetrics() {
    const nodes=snapshot().graph.nodes;
    const statuses=Object.entries(nodes).map(([id,node])=>nodeState(id,node).status);
    els.totalMetric.textContent=statuses.length;
    els.completeMetric.textContent=statuses.filter(x=>x==='complete').length;
    els.eligibleMetric.textContent=statuses.filter(x=>x==='eligible').length;
    els.terminalMetric.textContent=state.mode==='implementation' && snapshot().state?.terminal_goal_satisfied ? '達成':'未達成';
  }
  function renderDetails() {
    const snap=snapshot(),nodes=snap.graph.nodes;
    if (!state.selected || !nodes[state.selected]) state.selected=Object.keys(nodes)[0];
    const id=state.selected,node=nodes[id],info=meta(id,node),ns=nodeState(id,node);
    els.detailTitle.textContent=`${info.title} — ${info.subtitle}`;
    els.detailStatus.textContent=LABELS[ns.status]||ns.status;
    els.detailStatus.style.borderColor=statusColor(ns.status);
    els.detailDescription.textContent=description(id,node);
    els.detailGate.textContent=gate(id,node);

    els.detailDependencies.replaceChildren();
    const dependencies=node.depends_on||[];
    if (!dependencies.length) {
      const empty=document.createElement('span'); empty.className='empty-text'; empty.textContent='なし'; els.detailDependencies.appendChild(empty);
    } else {
      dependencies.forEach(dep=>{ const badge=document.createElement('span'); badge.className='node-badge'; badge.textContent=meta(dep,nodes[dep]||{}).title; els.detailDependencies.appendChild(badge); });
    }

    els.detailEvidence.replaceChildren();
    const evidence=node.required_evidence||RUNTIME_EVIDENCE[id]||[];
    if (!evidence.length) {
      const li=document.createElement('li'); li.className='empty-text'; li.textContent='追加Evidenceなし'; els.detailEvidence.appendChild(li);
    } else {
      evidence.forEach(key=>{ const recorded=Boolean(ns.evidence?.[key]); const li=document.createElement('li'); const mark=document.createElement('span'); mark.className=`evidence-mark${recorded?' is-recorded':''}`; mark.textContent=recorded?'✓':'○'; const text=document.createElement('span'); text.textContent=key; li.append(mark,text); els.detailEvidence.appendChild(li); });
    }

    els.projectStatus.textContent=state.mode==='implementation' ? snapshot().state?.project_status||'—' : snapshot().graph.status||'target_architecture';
    els.currentNode.textContent=state.mode==='implementation' ? snapshot().state?.current_node||'—' : '—';
    els.attemptCount.textContent=String(ns.attempts?.length||0);
    els.lastUpdated.textContent=STATIC ? 'Static Snapshot' : new Date(snapshot().generated_at_unix_ms).toLocaleTimeString('ja-JP');
  }
  function renderTabs() {
    els.implementationTab.classList.toggle('is-active',state.mode==='implementation');
    els.runtimeTab.classList.toggle('is-active',state.mode==='product-runtime');
  }
  function renderAll() { renderTabs(); renderMetrics(); renderGraph(); renderDetails(); }
  function selectNode(id) { state.selected=id; renderAll(); els.liveRegion.textContent=`${meta(id,snapshot().graph.nodes[id]).title}を選択しました`; }
  async function load(mode,preserve=true) {
    const snap=STATIC ? STATIC[mode] : await fetch(`/api/snapshot?graph=${encodeURIComponent(mode)}`,{cache:'no-store'}).then(r=>{ if(!r.ok) throw new Error(`snapshot_load_failed:${r.status}`); return r.json(); });
    state.snapshots[mode]=snap;
    if(!preserve || !state.selected || !snap.graph.nodes[state.selected]) state.selected=Object.keys(snap.graph.nodes)[0];
    renderAll();
  }
  async function switchMode(mode) {
    state.mode=mode; state.selected=null; state.query=''; state.filter='all';
    els.searchInput.value=''; els.statusFilter.value='all'; await load(mode,false);
  }

  els.implementationTab.addEventListener('click',()=>switchMode('implementation'));
  els.runtimeTab.addEventListener('click',()=>switchMode('product-runtime'));
  els.searchInput.addEventListener('input',e=>{state.query=e.target.value;renderGraph();});
  els.statusFilter.addEventListener('change',e=>{state.filter=e.target.value;renderGraph();});

  load('implementation',false).then(()=>{ if(!STATIC) state.refreshTimer=setInterval(()=>load(state.mode,true).catch(console.error),3000); }).catch(error=>{ els.detailTitle.textContent='Graphを読み込めませんでした'; els.detailDescription.textContent=String(error); });
})();
