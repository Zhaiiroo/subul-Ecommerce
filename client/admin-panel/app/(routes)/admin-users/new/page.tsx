import { PageContainer } from '@/components/layout/page-container';
import { PageHeader } from '@/components/layout/page-header';
import { AdminUserForm } from '@/features/admin-user';
import { messages } from '@/lib/messages.ar';

export const metadata = {
  title: messages.adminUser.form.newTitle,
};

export default function NewAdminUserPage() {
  return (
    <PageContainer>
      <div className="mx-auto flex w-full max-w-5xl flex-col gap-6">
        <PageHeader
          title={messages.adminUser.form.newTitle}
          description={messages.adminUser.form.newDescription}
        />
        <AdminUserForm />
      </div>
    </PageContainer>
  );
}
