import { FormSelect } from "@/components/ui/form-select";
import { createFileRoute } from "@tanstack/react-router";
import { Download, FileText, Upload } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/my-documents")({ component: MyDocuments });
type Row = Record<string, unknown>;

function MyDocuments() {
  const documents = useApi(async () => (await realApi.myDocuments()).items as Row[], []);
  const [file, setFile] = useState<File | null>(null);
  const [category, setCategory] = useState("id");
  const [title, setTitle] = useState("");
  const [busy, setBusy] = useState(false);

  const upload = async () => {
    if (!file || !title.trim()) return;
    setBusy(true);
    try {
      await realApi.uploadMyDocument(file, category, title.trim());
      toast.success("Document uploaded", {
        description: "It is now part of your personal HR file.",
      });
      setFile(null);
      setTitle("");
      documents.reload();
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "Document upload failed");
    } finally {
      setBusy(false);
    }
  };

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Employee self-service"
          title="My documents"
          description="View and download your own HR file, or submit a personal document for HR review. Restricted investigation evidence is never shown here."
        />
        <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_22rem]" data-testid="my-documents">
          <Card>
            <CardHeader>
              <CardTitle>Personal file</CardTitle>
              <CardDescription>
                Only documents attached to your linked worker record.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <Async state={documents}>
                {(rows) => (
                  <ul className="divide-y rounded-md border">
                    {rows.map((document) => (
                      <li key={String(document.id)} className="flex items-center gap-3 p-3">
                        <FileText className="size-4 text-muted-foreground" aria-hidden />
                        <span className="min-w-0 flex-1">
                          <strong className="block truncate text-sm">
                            {String(document.title)}
                          </strong>
                          <span className="text-xs text-muted-foreground">
                            {String(document.category)} · {String(document.fileName)}
                          </span>
                        </span>
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() =>
                            realApi
                              .downloadMyDocument(String(document.id), String(document.fileName))
                              .catch((error) =>
                                toast.error(
                                  error instanceof Error ? error.message : "Download failed",
                                ),
                              )
                          }
                        >
                          <Download className="size-4" aria-hidden /> Download
                        </Button>
                      </li>
                    ))}
                    {rows.length === 0 ? (
                      <li className="p-6 text-center text-sm text-muted-foreground">
                        No personal documents yet.
                      </li>
                    ) : null}
                  </ul>
                )}
              </Async>
            </CardContent>
          </Card>
          <Card>
            <CardHeader>
              <CardTitle>Upload a document</CardTitle>
              <CardDescription>PDF, Word, or image files up to 25 MB.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <div>
                <Label htmlFor="personal-document-category">Category</Label>
                <FormSelect
                  id="personal-document-category"
                  className="h-9 w-full rounded-md border bg-transparent px-3 text-sm"
                  value={category}
                  onChange={(event) => setCategory(event.target.value)}
                >
                  <option value="id">Identity</option>
                  <option value="qualification">Qualification</option>
                  <option value="medical">Medical</option>
                  <option value="certificate">Certificate</option>
                </FormSelect>
              </div>
              <div>
                <Label htmlFor="personal-document-title">Title</Label>
                <Input
                  id="personal-document-title"
                  value={title}
                  onChange={(event) => setTitle(event.target.value)}
                />
              </div>
              <div>
                <Label htmlFor="personal-document-file">File</Label>
                <Input
                  id="personal-document-file"
                  type="file"
                  onChange={(event) => setFile(event.target.files?.[0] ?? null)}
                />
              </div>
              <Button onClick={upload} disabled={busy || !file || !title.trim()}>
                <Upload className="size-4" aria-hidden /> {busy ? "Uploading…" : "Upload"}
              </Button>
            </CardContent>
          </Card>
        </div>
      </AppShell>
    </AuthGate>
  );
}
