// Chinese labels for tools and states. Kept in one place so the timeline, the
// inspector and the tests agree on the wording.
export const TOOL_TITLES = {
  file_info: '文件信息', register_conversation: '登记对话', exec_command: '执行命令', read_file: '读取文件',
  write_file: '写入文件', edit_file: '编辑文件', apply_patch: '应用补丁', git_status: 'Git 状态',
  git_diff: 'Git 差异', search_text: '搜索内容', search_files: '搜索文件', open_workspace: '打开工作区',
  update_plan: '更新计划', list_directory: '浏览目录', read_command: '读取输出', write_stdin: '继续命令',
  poll_command: '读取输出', get_workspace_status: '工作区状态', render_workspace: '打开面板',
  read_workspace_activity: '同步活动', stop_command: '停止命令', read_image: '查看图片',
  create_directory: '新建目录', show_changes: '工具改动',
};

export const STATES = { running: '进行中', failed: '失败', returned: '已返回' };
export const OPERATIONS = { add: '新建', replace: '替换', edit: '精确替换', update: '修改', move: '移动', delete: '删除' };
export const KIND_ICON = { write: 'filePen', read: 'fileText', search: 'search', command: 'terminal', list: 'folder', text: 'git', plan: 'listChecks', info: 'info', none: 'info' };
// One accent per call type, so the timeline reads by what was done instead of by status.
export const KIND_TONE = { write: 'write', read: 'read', search: 'search', command: 'command', list: 'list', text: 'git', plan: 'plan', info: 'info', none: 'info' };

export const titleOf = tool => TOOL_TITLES[tool] || tool || '调用';
export const stateOf = status => STATES[status] || status || '';
export const toneOf = kind => KIND_TONE[kind] || 'info';
export const iconOf = kind => KIND_ICON[kind] || 'info';
