import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';

// Produces the registration instruction the user pastes into the matching ChatGPT chat.
export function SetupDialog({ open, onOpenChange }) {
  const [title, setTitle] = useState('');
  const [path, setPath] = useState('');
  const [chat, setChat] = useState('');
  const [prompt, setPrompt] = useState('');
  const [note, setNote] = useState('');

  const generate = () => {
    if (!title.trim() || !path.trim()) { setNote('请填写对话名称和工作目录。'); return; }
    let chatId = '';
    if (chat.trim()) {
      try {
        const url = new URL(chat.trim());
        if (url.origin !== 'https://chatgpt.com' || !/^\/c\/[0-9a-f-]{36}$/i.test(url.pathname)) throw Error('bad');
        chatId = url.pathname.slice(3);
      } catch {
        setNote('请填写真实的 https://chatgpt.com/c/ 对话链接，或留空。');
        return;
      }
    }
    const args = { title: title.trim(), path: path.trim(), ...(chatId ? { chat_id: chatId } : {}) };
    setPrompt('请先调用本地工作区的 register_conversation，参数为 ' + JSON.stringify(args) +
      '。先告诉我登记的对话名称与 dashboard_url，再开始操作；本对话后续每次工具调用都携带返回的 thread_id，不复用其他聊天的 ID。');
    setNote('把下面的指令发送到对应 ChatGPT 对话；登记后左侧自动出现线程。');
  };

  const copy = async () => {
    if (!prompt) return;
    try { await navigator.clipboard.writeText(prompt); setNote('指令已复制。'); }
    catch { setNote('复制失败，请手动选中指令文本。'); }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[480px]">
        <DialogHeader>
          <DialogTitle>登记对话</DialogTitle>
          <DialogDescription>填写名称与目录，生成要发送给对应 ChatGPT 对话的登记指令。</DialogDescription>
        </DialogHeader>
        <div className="grid gap-3">
          <div className="grid gap-1.5">
            <Label htmlFor="new-title">对话名称</Label>
            <Input id="new-title" value={title} onChange={event => setTitle(event.target.value)} placeholder="例如：优化自主交易员页面" />
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="new-path">工作目录</Label>
            <Input id="new-path" value={path} onChange={event => setPath(event.target.value)} placeholder="E:/my_space/your-project" />
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="new-chat">ChatGPT 对话链接（可选）</Label>
            <Input id="new-chat" value={chat} onChange={event => setChat(event.target.value)} placeholder="https://chatgpt.com/c/…" />
          </div>
          <Button id="generate" className="w-full" onClick={generate}>生成登记指令</Button>
          <Textarea id="prompt" readOnly value={prompt} aria-label="对话登记指令" className="h-24 text-[11px] leading-relaxed"
            placeholder="填写名称和目录，生成后复制到对应的 ChatGPT 对话。" />
          <Button id="copy-prompt" variant="outline" onClick={copy}>复制指令</Button>
          {note ? <p id="setup-note" className="text-[11px] text-muted-foreground">{note}</p> : null}
        </div>
      </DialogContent>
    </Dialog>
  );
}
