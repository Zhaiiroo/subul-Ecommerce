import { notFound } from 'next/navigation';
import { PageContainer } from '@/components/layout/page-container';
import { AdminUserView, getCachedAdminUserById } from '@/features/admin-user';
import { messages } from '@/lib/messages.ar';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const adminUserId = parseInt(id, 10);

  if (isNaN(adminUserId) || adminUserId <= 0) {
    return { title: messages.adminUser.view.title };
  }

  const adminUser = await getCachedAdminUserById(adminUserId).catch(() => null);

  return {
    title: adminUser
      ? messages.adminUser.view.pageTitle(adminUser.name)
      : messages.adminUser.view.title,
  };
}

interface ViewAdminUserPageProps {
  params: Promise<{ id: string }>;
}

export default async function ViewAdminUserPage({ params }: ViewAdminUserPageProps) {
  const { id } = await params;
  const adminUserId = parseInt(id, 10);

  if (isNaN(adminUserId) || adminUserId <= 0) {
    notFound();
  }

  const adminUser = await getCachedAdminUserById(adminUserId).catch(() => null);

  if (!adminUser) {
    notFound();
  }

  return (
    <PageContainer>
      <div className="mx-auto flex w-full max-w-5xl flex-col gap-6">
        <AdminUserView adminUser={adminUser} />
      </div>
    </PageContainer>
  );
}
