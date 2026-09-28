import { notFound } from 'next/navigation';
import { PageContainer } from '@/components/layout/page-container';
import { PageHeader } from '@/components/layout/page-header';
import { AdminUserForm, getAdminUserById } from '@/features/admin-user';
import { messages } from '@/lib/messages.ar';

export const metadata = {
  title: messages.adminUser.form.editTitle,
};

interface EditAdminUserPageProps {
  params: Promise<{ id: string }>;
}

export default async function EditAdminUserPage({ params }: EditAdminUserPageProps) {
  const { id } = await params;
  const adminUserId = parseInt(id, 10);

  if (isNaN(adminUserId) || adminUserId <= 0) {
    notFound();
  }

  const adminUser = await getAdminUserById(adminUserId).catch(() => null);

  if (!adminUser) {
    notFound();
  }

  return (
    <PageContainer>
      <div className="mx-auto flex w-full max-w-5xl flex-col gap-6">
        <PageHeader
          title={messages.adminUser.form.editTitle}
          description={adminUser.name}
        />
        <AdminUserForm initialData={adminUser} />
      </div>
    </PageContainer>
  );
}
